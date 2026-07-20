using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class Conversation
{
    [Key]
    public int ConversationId { get; set; }

    public int StudentId { get; set; }
    public int TeacherId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public User Student { get; set; } = null!;
    public User Teacher { get; set; } = null!;

    public ICollection<ChatMessage> Messages { get; set; } =
        new List<ChatMessage>();
}
