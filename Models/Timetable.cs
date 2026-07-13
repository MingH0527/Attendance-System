using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class Timetable
{
    [Key]
    public int TimetableId { get; set; }

    [Required]
    public int TeacherId { get; set; }

    [Required]
    public int CourseId { get; set; }

    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [Required]
    public int ClassId { get; set; }

    [ForeignKey(nameof(ClassId))]
    public ClassGroup? ClassGroup { get; set; }

    public int? ClassroomId { get; set; }

    [ForeignKey(nameof(ClassroomId))]
    public Classroom? Classroom { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    public ClassType ClassType { get; set; }
}