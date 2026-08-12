using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Hubs;

[Authorize(Roles = "Student,Teacher")]
public class ChatHub : Hub
{
    private readonly DB db;

    public ChatHub(DB db)
    {
        this.db = db;
    }

    // ==================================================
    // TEXT CHAT
    // ==================================================

    public async Task SendMessage(
        int otherUserId,
        string content)
    {
        int currentUserId =
            GetCurrentUserId();

        string currentRole =
            GetCurrentRole();

        content ??=
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                content))
        {
            throw new HubException(
                "Message cannot be empty."
            );
        }

        if (content.Length > 400)
        {
            throw new HubException(
                "Message cannot exceed 400 characters."
            );
        }

        User otherUser =
            await GetAllowedOtherUser(
                currentUserId,
                currentRole,
                otherUserId
            );

        int studentId =
            currentRole == "Student"
                ? currentUserId
                : otherUserId;

        int teacherId =
            currentRole == "Teacher"
                ? currentUserId
                : otherUserId;

        Conversation? conversation =
            await db.Conversations
                .FirstOrDefaultAsync(
                    c =>
                        c.StudentId ==
                            studentId &&

                        c.TeacherId ==
                            teacherId
                );

        DateTime now =
            DateTime.UtcNow;

        if (conversation == null)
        {
            conversation =
                new Conversation
                {
                    StudentId =
                        studentId,

                    TeacherId =
                        teacherId,

                    CreatedAt =
                        now,

                    LastMessageAt =
                        now
                };

            db.Conversations.Add(
                conversation
            );

            await db.SaveChangesAsync();
        }

        conversation.LastMessageAt =
            now;

        ChatMessage message =
            new()
            {
                ConversationId =
                    conversation
                        .ConversationId,

                SenderId =
                    currentUserId,

                ReceiverId =
                    otherUserId,

                Content =
                    content,

                SentAtUtc =
                    now,

                IsRead =
                    false
            };

        db.ChatMessages.Add(
            message
        );

        await db.SaveChangesAsync();

        ChatMessageDto dto =
            new()
            {
                MessageId =
                    message.ChatMessageId,

                ConversationId =
                    message.ConversationId,

                SenderId =
                    message.SenderId,

                ReceiverId =
                    message.ReceiverId,

                Content =
                    message.Content,

                SentAtUtc =
                    DateTime.SpecifyKind(
                        message.SentAtUtc,
                        DateTimeKind.Utc
                    ),

                IsRead =
                    message.IsRead
            };

        await Clients.Group(UserGroup(currentUserId))
            .SendAsync(
                "ReceiveMessage",
                dto
            );

        await Clients.Group(UserGroup(otherUserId))
            .SendAsync(
                "ReceiveMessage",
                dto
            );

        await NotifyConversationUpdated(
            currentUserId,
            otherUserId,
            conversation.ConversationId
        );
    }

    public async Task
        MarkConversationAsRead(
            int conversationId)
    {
        int currentUserId =
            GetCurrentUserId();

        bool belongsToUser =
            await db.Conversations
                .AnyAsync(
                    c =>
                        c.ConversationId ==
                            conversationId &&

                        (
                            c.StudentId ==
                                currentUserId ||

                            c.TeacherId ==
                                currentUserId
                        )
                );

        if (!belongsToUser)
        {
            throw new HubException(
                "Conversation not found."
            );
        }

        List<ChatMessage> unreadMessages =
            await db.ChatMessages
                .Where(
                    m =>
                        m.ConversationId ==
                            conversationId &&

                        m.ReceiverId ==
                            currentUserId &&

                        !m.IsRead
                )
                .ToListAsync();

        if (unreadMessages.Count == 0)
        {
            return;
        }

        DateTime now =
            DateTime.UtcNow;

        foreach (
            ChatMessage message
            in unreadMessages)
        {
            message.IsRead =
                true;

            message.ReadAtUtc =
                now;
        }

        await db.SaveChangesAsync();

        int otherUserId =
            await db.Conversations
                .Where(
                    c =>
                        c.ConversationId ==
                            conversationId
                )
                .Select(
                    c =>
                        c.StudentId ==
                            currentUserId
                            ? c.TeacherId
                            : c.StudentId
                )
                .SingleAsync();

        await Clients.Group(UserGroup(currentUserId))
            .SendAsync(
                "MessagesRead",
                conversationId,
                currentUserId
            );

        await Clients.Group(UserGroup(otherUserId))
            .SendAsync(
                "MessagesRead",
                conversationId,
                currentUserId
            );

        await NotifyConversationUpdated(
            currentUserId,
            otherUserId,
            conversationId
        );
    }

    // ==================================================
    // VIDEO CALL SIGNALING + DATABASE CALL RECORD
    // ==================================================

    public async Task<long> StartVideoCall(int otherUserId)
    {
        int currentUserId = GetCurrentUserId();
        string currentRole = GetCurrentRole();

        User currentUser = await db.Users
            .AsNoTracking()
            .SingleAsync(u => u.UserId == currentUserId);

        await GetAllowedOtherUser(currentUserId, currentRole, otherUserId);

        Conversation conversation = await GetOrCreateConversation(
            currentUserId,
            currentRole,
            otherUserId
        );

        DateTime now = DateTime.UtcNow;

        ChatMessage callMessage = new()
        {
            ConversationId = conversation.ConversationId,
            SenderId = currentUserId,
            ReceiverId = otherUserId,
            Content = "Video call",
            MessageType = "VideoCall",
            CallStatus = "Calling",
            CallStartedAtUtc = now,
            SentAtUtc = now,
            IsRead = false
        };

        conversation.LastMessageAt = now;
        db.ChatMessages.Add(callMessage);
        await db.SaveChangesAsync();

        ChatMessageDto dto = ToDto(callMessage);

        await Clients.Group(UserGroup(currentUserId))
            .SendAsync("ReceiveMessage", dto);
        await Clients.Group(UserGroup(otherUserId))
            .SendAsync("ReceiveMessage", dto);

        await Clients.Group(UserGroup(otherUserId))
            .SendAsync("IncomingVideoCall", new
            {
                callerId = currentUserId,
                callerName = currentUser.Username,
                callerAvatar = string.IsNullOrWhiteSpace(currentUser.Avatar)
                    ? "default.png"
                    : currentUser.Avatar,
                callMessageId = callMessage.ChatMessageId
            });

        await NotifyConversationUpdated(
            currentUserId,
            otherUserId,
            conversation.ConversationId
        );

        return callMessage.ChatMessageId;
    }

    public async Task AcceptVideoCall(long callMessageId, int callerId)
    {
        int currentUserId = GetCurrentUserId();
        string currentRole = GetCurrentRole();

        await GetAllowedOtherUser(currentUserId, currentRole, callerId);

        ChatMessage call = await GetCallMessage(callMessageId, callerId, currentUserId);
        DateTime now = DateTime.UtcNow;

        call.CallStatus = "Connected";
        call.CallAnsweredAtUtc = now;
        await db.SaveChangesAsync();

        await BroadcastCallRecord(call);

        await Clients.Group(UserGroup(callerId))
            .SendAsync("VideoCallAccepted", currentUserId, callMessageId, now);
    }

    public async Task RejectVideoCall(long callMessageId, int callerId)
    {
        int currentUserId = GetCurrentUserId();
        string currentRole = GetCurrentRole();

        await GetAllowedOtherUser(currentUserId, currentRole, callerId);

        ChatMessage call = await GetCallMessage(callMessageId, callerId, currentUserId);
        DateTime now = DateTime.UtcNow;

        call.CallStatus = "Declined";
        call.CallEndedAtUtc = now;
        call.CallDurationSeconds = 0;
        await db.SaveChangesAsync();

        await BroadcastCallRecord(call);

        await Clients.Group(UserGroup(callerId))
            .SendAsync("VideoCallRejected", currentUserId, callMessageId);
    }

    public async Task SendWebRtcOffer(int otherUserId, string offerJson)
    {
        await ValidateCallTarget(otherUserId);
        await Clients.Group(UserGroup(otherUserId))
            .SendAsync("ReceiveWebRtcOffer", GetCurrentUserId(), offerJson);
    }

    public async Task SendWebRtcAnswer(int otherUserId, string answerJson)
    {
        await ValidateCallTarget(otherUserId);
        await Clients.Group(UserGroup(otherUserId))
            .SendAsync("ReceiveWebRtcAnswer", GetCurrentUserId(), answerJson);
    }

    public async Task SendIceCandidate(int otherUserId, string candidateJson)
    {
        await ValidateCallTarget(otherUserId);
        await Clients.Group(UserGroup(otherUserId))
            .SendAsync("ReceiveIceCandidate", GetCurrentUserId(), candidateJson);
    }

    public async Task EndVideoCall(long callMessageId, int otherUserId)
    {
        int currentUserId = GetCurrentUserId();
        await ValidateCallTarget(otherUserId);

        ChatMessage? call = await db.ChatMessages
            .FirstOrDefaultAsync(m =>
                m.ChatMessageId == callMessageId &&
                m.MessageType == "VideoCall" &&
                ((m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                 (m.SenderId == otherUserId && m.ReceiverId == currentUserId)));

        if (call != null && call.CallStatus is not ("Declined" or "Completed" or "Missed"))
        {
            DateTime now = DateTime.UtcNow;
            call.CallEndedAtUtc = now;

            if (call.CallAnsweredAtUtc.HasValue)
            {
                call.CallStatus = "Completed";
                call.CallDurationSeconds = Math.Max(
                    0,
                    (int)Math.Floor((now - call.CallAnsweredAtUtc.Value).TotalSeconds)
                );
            }
            else
            {
                call.CallStatus = "Missed";
                call.CallDurationSeconds = 0;
            }

            await db.SaveChangesAsync();
            await BroadcastCallRecord(call);
        }

        await Clients.Group(UserGroup(otherUserId))
            .SendAsync("VideoCallEnded", currentUserId, callMessageId);
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private async Task ValidateCallTarget(
        int otherUserId)
    {
        int currentUserId =
            GetCurrentUserId();

        string currentRole =
            GetCurrentRole();

        await GetAllowedOtherUser(
            currentUserId,
            currentRole,
            otherUserId
        );
    }

    private async Task<Conversation> GetOrCreateConversation(
        int currentUserId,
        string currentRole,
        int otherUserId)
    {
        int studentId = currentRole == "Student" ? currentUserId : otherUserId;
        int teacherId = currentRole == "Teacher" ? currentUserId : otherUserId;

        Conversation? conversation = await db.Conversations
            .FirstOrDefaultAsync(c =>
                c.StudentId == studentId &&
                c.TeacherId == teacherId);

        if (conversation != null)
        {
            return conversation;
        }

        DateTime now = DateTime.UtcNow;
        conversation = new Conversation
        {
            StudentId = studentId,
            TeacherId = teacherId,
            CreatedAt = now,
            LastMessageAt = now
        };

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    private async Task<ChatMessage> GetCallMessage(
        long callMessageId,
        int callerId,
        int receiverId)
    {
        ChatMessage? call = await db.ChatMessages
            .FirstOrDefaultAsync(m =>
                m.ChatMessageId == callMessageId &&
                m.MessageType == "VideoCall" &&
                m.SenderId == callerId &&
                m.ReceiverId == receiverId);

        if (call == null)
        {
            throw new HubException("Video call record was not found.");
        }

        return call;
    }

    private async Task BroadcastCallRecord(ChatMessage call)
    {
        ChatMessageDto dto = ToDto(call);

        await Clients.Group(UserGroup(call.SenderId))
            .SendAsync("CallRecordUpdated", dto);
        await Clients.Group(UserGroup(call.ReceiverId))
            .SendAsync("CallRecordUpdated", dto);

        await NotifyConversationUpdated(
            call.SenderId,
            call.ReceiverId,
            call.ConversationId
        );
    }

    private static ChatMessageDto ToDto(ChatMessage message)
    {
        return new ChatMessageDto
        {
            MessageId = message.ChatMessageId,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAtUtc = DateTime.SpecifyKind(message.SentAtUtc, DateTimeKind.Utc),
            IsRead = message.IsRead,
            MessageType = message.MessageType,
            CallStatus = message.CallStatus,
            CallStartedAtUtc = message.CallStartedAtUtc,
            CallAnsweredAtUtc = message.CallAnsweredAtUtc,
            CallEndedAtUtc = message.CallEndedAtUtc,
            CallDurationSeconds = message.CallDurationSeconds
        };
    }

    private async Task<User>
        GetAllowedOtherUser(
            int currentUserId,
            string currentRole,
            int otherUserId)
    {
        User? otherUser =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                            otherUserId &&

                        u.Status ==
                            "Active"
                );

        if (otherUser == null)
        {
            throw new HubException(
                "The selected account could not be found."
            );
        }

        bool allowed =
            currentRole == "Student" &&
            otherUser.Position == "Teacher" ||

            currentRole == "Teacher" &&
            otherUser.Position == "Student";

        if (!allowed ||
            currentUserId == otherUserId)
        {
            throw new HubException(
                "You are not allowed to contact this account."
            );
        }

        return otherUser;
    }

    private async Task
        NotifyConversationUpdated(
            int firstUserId,
            int secondUserId,
            int conversationId)
    {
        await Clients.Group(UserGroup(firstUserId))
            .SendAsync(
                "ConversationUpdated",
                conversationId
            );

        await Clients.Group(UserGroup(secondUserId))
            .SendAsync(
                "ConversationUpdated",
                conversationId
            );
    }

    private int GetCurrentUserId()
    {
        string? value =
            Context.User?
                .FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

        if (!int.TryParse(
                value,
                out int userId))
        {
            throw new HubException(
                "Your login session has expired."
            );
        }

        return userId;
    }

    private string GetCurrentRole()
    {
        string? role =
            Context.User?
                .FindFirstValue(
                    ClaimTypes.Role
                );

        if (role is not
            ("Student" or "Teacher"))
        {
            throw new HubException(
                "This account cannot use chat."
            );
        }

        return role;
    }
    public override async Task OnConnectedAsync()
    {
        int currentUserId =
            GetCurrentUserId();

        string groupName =
            UserGroup(
                currentUserId
            );

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            groupName
        );

        Console.WriteLine(
            $"SignalR connected: " +
            $"ConnectionId={Context.ConnectionId}, " +
            $"ClaimUserId={currentUserId}, " +
            $"SignalRUserId={Context.UserIdentifier}, " +
            $"Group={groupName}"
        );

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        string? value =
            Context.User?
                .FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

        if (int.TryParse(
                value,
                out int currentUserId))
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                UserGroup(
                    currentUserId
                )
            );
        }

        Console.WriteLine(
            $"SignalR disconnected: " +
            $"ConnectionId={Context.ConnectionId}, " +
            $"UserId={Context.UserIdentifier}, " +
            $"Error={exception?.Message}"
        );

        await base.OnDisconnectedAsync(
            exception
        );
    }

    private static string UserGroup(
        int userId)
    {
        return $"user-{userId}";
    }
}