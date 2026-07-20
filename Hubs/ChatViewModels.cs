namespace WebAndMobile.ViewModels;

public class ChatIndexViewModel
{
    public int CurrentUserId { get; set; }
    public string CurrentRole { get; set; } = string.Empty;
    public int? SelectedUserId { get; set; }
    public List<ConversationListItemViewModel> Conversations { get; set; } = [];
}

public class ConversationListItemViewModel
{
    public int ConversationId { get; set; }
    public int OtherUserId { get; set; }
    public string OtherUsername { get; set; } = string.Empty;
    public string OtherAvatar { get; set; } = "default.png";
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
}

public class ChatMessageDto
{
    public long MessageId { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public bool IsRead { get; set; }
}
