using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class ChatMessage
{
    [Key]
    public long ChatMessageId { get; set; }

    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }

    [Required]
    [MaxLength(400)]
    public string Content { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    [MaxLength(20)]
    public string MessageType { get; set; } = "Text";

    [MaxLength(20)]
    public string? CallStatus { get; set; }

    public DateTime? CallStartedAtUtc { get; set; }
    public DateTime? CallAnsweredAtUtc { get; set; }
    public DateTime? CallEndedAtUtc { get; set; }
    public int? CallDurationSeconds { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}
