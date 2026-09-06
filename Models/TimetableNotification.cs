using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class TimetableNotification
{
    [Key, MaxLength(20)] public string NotificationId { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string Type { get; set; } = string.Empty;
    [Required, MaxLength(250)] public string Message { get; set; } = string.Empty;
    [MaxLength(20)] public string? TermId { get; set; }
    [MaxLength(20)] public string? TimetableId { get; set; }
    [MaxLength(20)] public string? UserId { get; set; }
    public DateTime? OccurrenceDate { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
