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

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}
