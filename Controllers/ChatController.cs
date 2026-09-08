using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Filters;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Student,Teacher")]
[RequireStudentProfile]
public class ChatController : Controller
{
    private readonly DB db;
    private readonly IAvatarService avatarService;

    public ChatController(DB db, IAvatarService avatarService)
    {
        this.db = db;
        this.avatarService = avatarService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? userId)
    {
        string currentUserId =
            GetCurrentUserId();

        string currentRole =
            GetCurrentRole();

        User currentUser =
            await db.Users
                .AsNoTracking()
                .SingleAsync(
                    u => u.UserId == currentUserId
                );

        ViewBag.CurrentUserName = currentUser.Username;
        ViewBag.CurrentUserAvatar =
            avatarService.ResolveAvatar(currentUser.UserId, currentUser.Avatar);

        ChatIndexViewModel model =
            new()
            {
                CurrentUserId =
                    currentUserId,

                CurrentRole =
                    currentRole,

                SelectedUserId =
                    userId,

                Conversations =
                    await BuildConversationList(
                        currentUserId
                    )
            };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> SearchUsers(
        string query)
    {
        string currentUserId =
            GetCurrentUserId();

        string currentRole =
            GetCurrentRole();

        query =
            (query ?? string.Empty)
                .Trim();

        if (query.Length < 1)
        {
            return Json(
                Array.Empty<object>()
            );
        }

        string requiredRole =
            currentRole == "Student"
                ? "Teacher"
                : "Student";

        List<User> users =
            await db.Users
                .AsNoTracking()
                .Where(
                    u =>
                        u.UserId != currentUserId &&
                        u.Position == requiredRole &&
                        u.Status == "Active" &&
                        EF.Functions.Like(u.Username, $"%{query}%")
                )
                .OrderBy(u => u.Username)
                .Take(10)
                .ToListAsync();

        var results = users.Select(u => new
        {
            userId = u.UserId,
            username = u.Username,
            avatar = avatarService.ResolveAvatar(u.UserId, u.Avatar),
            position = u.Position
        });

        return Json(results);
    }

    [HttpGet]
    public async Task<IActionResult> GetConversation(
        string otherUserId,
        string? beforeMessageId = null)
    {
        string currentUserId =
            GetCurrentUserId();

        string currentRole =
            GetCurrentRole();

        User? otherUser =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId == otherUserId &&
                        u.Status == "Active"
                );

        if (otherUser == null)
        {
            return NotFound(
                new
                {
                    message =
                        "User not found."
                }
            );
        }

        bool allowed =
            currentRole == "Student" &&
            otherUser.Position == "Teacher" ||

            currentRole == "Teacher" &&
            otherUser.Position == "Student";

        Conversation? conversation;

        if (allowed)
        {
            string studentId =
                currentRole == "Student"
                    ? currentUserId
                    : otherUserId;

            string teacherId =
                currentRole == "Teacher"
                    ? currentUserId
                    : otherUserId;

            conversation =
                await db.Conversations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        c =>
                            c.StudentId == studentId &&
                            c.TeacherId == teacherId
                    );
        }
        else
        {
            // A legacy conversation can remain after an account role changes.
            // Keep the history visible, but the client will lock messaging/calls.
            conversation =
                await db.Conversations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        c =>
                            (c.StudentId == currentUserId && c.TeacherId == otherUserId) ||
                            (c.StudentId == otherUserId && c.TeacherId == currentUserId)
                    );
        }

        if (conversation == null)
        {
            return Json(
                new
                {
                    conversationId =
                        string.Empty,

                    otherUser =
                        new
                        {
                            userId =
                                otherUser.UserId,

                            username =
                                otherUser.Username,

                            avatar =
                                avatarService.ResolveAvatar(
                                    otherUser.UserId,
                                    otherUser.Avatar
                                ),

                            position =
                                otherUser.Position
                        },

                    messages =
                        Array.Empty<object>(),

                    hasMore =
                        false,

                    canChat =
                        allowed
                }
            );
        }

        IQueryable<ChatMessage> messageQuery =
            db.ChatMessages
                .AsNoTracking()
                .Where(
                    m =>
                        m.ConversationId ==
                        conversation.ConversationId
                );

        if (!string.IsNullOrWhiteSpace(beforeMessageId))
        {
            messageQuery =
                messageQuery.Where(
                    m =>
                        string.Compare(
                            m.ChatMessageId,
                            beforeMessageId
                        ) < 0
                );
        }

        List<ChatMessage> messageEntities =
     await messageQuery
         .Include(m => m.Sender)
         .OrderByDescending(
             m => m.ChatMessageId
         )
         .Take(30)
         .ToListAsync();

        messageEntities.Reverse();

        List<ChatMessageDto> messages =
            messageEntities
                .Select(
                    m => new ChatMessageDto
                    {
                        MessageId =
                            m.ChatMessageId,

                        ConversationId =
                            m.ConversationId,

                        SenderId =
                            m.SenderId,

                        ReceiverId =
                            m.ReceiverId,

                        SenderName =
                        m.Sender.Username,

                        SenderAvatar =
                        avatarService.ResolveAvatar(
                            m.Sender.UserId,
                            m.Sender.Avatar
                        ),

                        Content =
                            m.Content,

                        SentAtUtc =
                            EnsureUtc(
                                m.SentAtUtc
                            ),

                        IsRead =
                            m.IsRead,

                        MessageType =
                            m.MessageType,

                        CallStatus =
                            m.CallStatus,

                        CallStartedAtUtc =
                            m.CallStartedAtUtc,

                        CallAnsweredAtUtc =
                            m.CallAnsweredAtUtc,

                        CallEndedAtUtc =
                            m.CallEndedAtUtc,

                        CallDurationSeconds =
                            m.CallDurationSeconds
                    }
                )
                .ToList();

        return Json(
            new
            {
                conversationId =
                    conversation.ConversationId,

                otherUser =
                    new
                    {
                        userId =
                            otherUser.UserId,

                        username =
                            otherUser.Username,

                        avatar =
                            avatarService.ResolveAvatar(
                                otherUser.UserId,
                                otherUser.Avatar
                            ),

                        position =
                            otherUser.Position
                    },

                messages,

                hasMore =
                    messages.Count == 30,

                canChat =
                    allowed
            }
        );
    }

    [HttpGet]
    public async Task<IActionResult>
        ConversationList()
    {
        string currentUserId =
            GetCurrentUserId();

        User currentUser =
              await db.Users
                .AsNoTracking()
                .SingleAsync(
                   u => u.UserId == currentUserId
           );

        return Json(
            await BuildConversationList(
                currentUserId
            )
        );
    }

    private async Task<
        List<ConversationListItemViewModel>>
        BuildConversationList(
            string currentUserId)
    {
        string currentRole =
            GetCurrentRole();

        List<Conversation> conversations =
            await db.Conversations
                .AsNoTracking()
                .Where(
                    c =>
                        c.StudentId ==
                            currentUserId ||

                        c.TeacherId ==
                            currentUserId
                )
                .Include(
                    c => c.Student
                )
                .Include(
                    c => c.Teacher
                )
                .OrderByDescending(
                    c => c.LastMessageAt
                )
                .ToListAsync();

        if (conversations.Count == 0)
        {
            return [];
        }

        List<string> conversationIds =
            conversations
                .Select(
                    c => c.ConversationId
                )
                .ToList();

        Dictionary<string, ChatMessage>
            latestMessages =
                await db.ChatMessages
                    .AsNoTracking()
                    .Where(
                        m =>
                            conversationIds.Contains(
                                m.ConversationId
                            )
                    )
                    .GroupBy(
                        m => m.ConversationId
                    )
                    .Select(
                        g =>
                            g.OrderByDescending(
                                m => m.ChatMessageId
                            )
                            .First()
                    )
                    .ToDictionaryAsync(
                        m => m.ConversationId
                    );

        Dictionary<string, int>
            unreadCounts =
                await db.ChatMessages
                    .AsNoTracking()
                    .Where(
                        m =>
                            conversationIds.Contains(
                                m.ConversationId
                            ) &&

                            m.ReceiverId ==
                                currentUserId &&

                            !m.IsRead
                    )
                    .GroupBy(
                        m => m.ConversationId
                    )
                    .Select(
                        g => new
                        {
                            ConversationId =
                                g.Key,

                            Count =
                                g.Count()
                        }
                    )
                    .ToDictionaryAsync(
                        x => x.ConversationId,
                        x => x.Count
                    );

        return conversations
            .Select(
                c =>
                {
                    User other =
                        c.StudentId ==
                            currentUserId
                            ? c.Teacher
                            : c.Student;

                    latestMessages.TryGetValue(
                        c.ConversationId,
                        out ChatMessage? latest
                    );

                    DateTime lastTime =
                        latest?.SentAtUtc ??
                        c.LastMessageAt;

                    return new
                        ConversationListItemViewModel
                    {
                        ConversationId =
                            c.ConversationId,

                        OtherUserId =
                            other.UserId,

                        OtherUsername =
                            other.Username,

                        OtherAvatar =
                            avatarService.ResolveAvatar(
                                other.UserId,
                                other.Avatar
                            ),

                        OtherPosition =
                            other.Position,

                        CanChat =
                            (currentRole == "Student" && other.Position == "Teacher") ||
                            (currentRole == "Teacher" && other.Position == "Student"),

                        LastMessage =
                            latest?.Content ??
                            string.Empty,

                        LastMessageAtUtc =
                            EnsureUtc(
                                lastTime
                            ),

                        UnreadCount =
                            unreadCounts
                                .GetValueOrDefault(
                                    c.ConversationId
                                )
                    };
                }
            )
            .ToList();
    }

    private string GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException(
                "Login session expired."
            );
        }

        return userId;
    }

    private string GetCurrentRole()
    {
        return User.FindFirstValue(
            ClaimTypes.Role
        ) ?? string.Empty;
    }

    private static DateTime EnsureUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc
                )
        };
    }
}
