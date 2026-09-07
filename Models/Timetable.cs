using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class Timetable
{
    [MaxLength(20)]
    public string? TermId { get; set; }

    [ForeignKey(nameof(TermId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AcademicTerm? Term { get; set; }

    [Key]
    [MaxLength(20)]
    public string TimetableId { get; set; } =
        string.Empty;

    [Required]
    [MaxLength(20)]
    public string CourseId { get; set; } =
        string.Empty;

    [ForeignKey(nameof(CourseId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Course? Course { get; set; }

    [Required]
    [MaxLength(20)]
    public string ClassId { get; set; } =
        string.Empty;

    [ForeignKey(nameof(ClassId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ClassGroup? ClassGroup { get; set; }

    [MaxLength(20)]
    public string? ClassroomId { get; set; }

    [ForeignKey(nameof(ClassroomId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Classroom? Classroom { get; set; }

    [Required]
    [MaxLength(20)]
    public string TeacherId { get; set; } =
        string.Empty;

    [ForeignKey(nameof(TeacherId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User Teacher { get; set; } =
        null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public ClassType ClassType { get; set; }

    public ICollection<TimetableNotification>
        Notifications
    { get; set; } =
            new List<TimetableNotification>();
}