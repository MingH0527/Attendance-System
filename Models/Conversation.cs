using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(StudentId), nameof(TeacherId), IsUnique = true)]
public class Conversation
{
    [Key]
    [MaxLength(20)]
    public string ConversationId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string StudentId { get; set; } = string.Empty;
    [MaxLength(20)]
    public string TeacherId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(StudentId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User Student { get; set; } = null!;

    [ForeignKey(nameof(TeacherId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User Teacher { get; set; } = null!;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
