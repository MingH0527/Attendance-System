using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class Course
{
    [Key]
    public int CourseId { get; set; }

    [Required]
    [MaxLength(150)]
    public string CourseName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? CourseCode { get; set; }

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}