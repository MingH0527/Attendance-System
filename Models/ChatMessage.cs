using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(ConversationId), nameof(ChatMessageId))]
[Index(nameof(ReceiverId), nameof(IsRead))]
public class ChatMessage
{
    [Key]
    [MaxLength(20)]
    public string ChatMessageId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ConversationId { get; set; } = string.Empty;
    [MaxLength(20)]
    public string SenderId { get; set; } = string.Empty;
    [MaxLength(20)]
    public string ReceiverId { get; set; } = string.Empty;

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

    [ForeignKey(nameof(ConversationId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Conversation Conversation { get; set; } = null!;

    [ForeignKey(nameof(SenderId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User Sender { get; set; } = null!;

    [ForeignKey(nameof(ReceiverId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User Receiver { get; set; } = null!;
}
