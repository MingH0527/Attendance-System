using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

[Index(nameof(CourseName), IsUnique = true)]
public class Course
{
    [Key]
    [MaxLength(20)]
    public string CourseId { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string CourseName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? CourseCode { get; set; }

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}
