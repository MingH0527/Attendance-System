namespace WebAndMobile.Models;

public class ChatIndexViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public string CurrentRole { get; set; } = string.Empty;
    public string? SelectedUserId { get; set; }
    public List<ConversationListItemViewModel> Conversations { get; set; } = [];
}

public class ConversationListItemViewModel
{
    public string ConversationId { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUsername { get; set; } = string.Empty;
    public string OtherAvatar { get; set; } = "default.png";
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
}

public class ChatMessageDto
{
    public string MessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public bool IsRead { get; set; }
    public string MessageType { get; set; } = "Text";
    public string? CallStatus { get; set; }
    public DateTime? CallStartedAtUtc { get; set; }
    public DateTime? CallAnsweredAtUtc { get; set; }
    public DateTime? CallEndedAtUtc { get; set; }
    public int? CallDurationSeconds { get; set; }
}
