using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Student,Teacher")]
public class ChatController : Controller
{
    private readonly DB db;

    public ChatController(DB db)
    {
        this.db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? userId)
    {
        int currentUserId =
            GetCurrentUserId();

        string currentRole =
            GetCurrentRole();

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
        int currentUserId =
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

        var users =
            await db.Users
                .AsNoTracking()
                .Where(
                    u =>
                        u.UserId != currentUserId &&

                        u.Position == requiredRole &&

                        u.Status == "Active" &&

                        EF.Functions.Like(
                            u.Username,
                            $"%{query}%"
                        )
                )
                .OrderBy(
                    u => u.Username
                )
                .Take(10)
                .Select(
                    u => new
                    {
                        userId =
                            u.UserId,

                        username =
                            u.Username,

                        avatar =
                            string.IsNullOrWhiteSpace(
                                u.Avatar
                            )
                                ? "default.png"
                                : u.Avatar,

                        position =
                            u.Position
                    }
                )
                .ToListAsync();

        return Json(users);
    }

    [HttpGet]
    public async Task<IActionResult> GetConversation(
        int otherUserId,
        long beforeMessageId = 0)
    {
        int currentUserId =
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

        if (!allowed)
        {
            return Forbid();
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
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.StudentId == studentId &&
                        c.TeacherId == teacherId
                );

        if (conversation == null)
        {
            return Json(
                new
                {
                    conversationId =
                        0,

                    otherUser =
                        new
                        {
                            userId =
                                otherUser.UserId,

                            username =
                                otherUser.Username,

                            avatar =
                                string.IsNullOrWhiteSpace(
                                    otherUser.Avatar
                                )
                                    ? "default.png"
                                    : otherUser.Avatar,

                            position =
                                otherUser.Position
                        },

                    messages =
                        Array.Empty<object>(),

                    hasMore =
                        false
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

        if (beforeMessageId > 0)
        {
            messageQuery =
                messageQuery.Where(
                    m =>
                        m.ChatMessageId <
                        beforeMessageId
                );
        }

        List<ChatMessage> messageEntities =
            await messageQuery
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
                            string.IsNullOrWhiteSpace(
                                otherUser.Avatar
                            )
                                ? "default.png"
                                : otherUser.Avatar,

                        position =
                            otherUser.Position
                    },

                messages,

                hasMore =
                    messages.Count == 30
            }
        );
    }

    [HttpGet]
    public async Task<IActionResult>
        ConversationList()
    {
        int currentUserId =
            GetCurrentUserId();

        return Json(
            await BuildConversationList(
                currentUserId
            )
        );
    }

    private async Task<
        List<ConversationListItemViewModel>>
        BuildConversationList(
            int currentUserId)
    {
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

        List<int> conversationIds =
            conversations
                .Select(
                    c => c.ConversationId
                )
                .ToList();

        Dictionary<int, ChatMessage>
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

        Dictionary<int, int>
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
                            string.IsNullOrWhiteSpace(
                                other.Avatar
                            )
                                ? "default.png"
                                : other.Avatar,

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

    private int GetCurrentUserId()
    {
        string? value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (!int.TryParse(
                value,
                out int userId))
        {
            throw new
                UnauthorizedAccessException(
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
