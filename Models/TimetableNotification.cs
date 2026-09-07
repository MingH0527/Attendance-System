using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class TimetableNotification
{
    [Key]
    [MaxLength(20)]
    public string NotificationId { get; set; } = string.Empty;

    [Required]
    [MaxLength(60)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? TermId { get; set; }

    [ForeignKey(nameof(TermId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AcademicTerm? Term { get; set; }

    [MaxLength(20)]
    public string? TimetableId { get; set; }

    [ForeignKey(nameof(TimetableId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Timetable? Timetable { get; set; }

    [MaxLength(20)]
    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    public DateTime? OccurrenceDate { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } =
        DateTime.Now;
}