using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Hubs;

[Authorize(Roles = "Student,Teacher")]
public class ChatHub : Hub
{
    private readonly DB db;

    public ChatHub(DB db)
    {
        this.db = db;
    }

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

        User? currentUser =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                            currentUserId &&

                        u.Status ==
                            "Active"
                );

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

        if (currentUser == null ||
            otherUser == null)
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

        if (!allowed)
        {
            throw new HubException(
                "You are not allowed to chat with this account."
            );
        }

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

        await Clients
            .User(
                currentUserId.ToString()
            )
            .SendAsync(
                "ReceiveMessage",
                dto
            );

        await Clients
            .User(
                otherUserId.ToString()
            )
            .SendAsync(
                "ReceiveMessage",
                dto
            );

        await Clients
            .User(
                currentUserId.ToString()
            )
            .SendAsync(
                "ConversationUpdated",
                conversation.ConversationId
            );

        await Clients
            .User(
                otherUserId.ToString()
            )
            .SendAsync(
                "ConversationUpdated",
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

        await Clients
            .User(
                currentUserId.ToString()
            )
            .SendAsync(
                "MessagesRead",
                conversationId,
                currentUserId
            );

        await Clients
            .User(
                otherUserId.ToString()
            )
            .SendAsync(
                "MessagesRead",
                conversationId,
                currentUserId
            );

        await Clients
            .User(
                currentUserId.ToString()
            )
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
}
