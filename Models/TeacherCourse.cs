using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(UserId), nameof(CourseId), IsUnique = true)]
public class TeacherCourse
{
    [Key]
    [MaxLength(20)]
    public string TeacherCourseId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User? User { get; set; }

    [MaxLength(20)]
    public string CourseId { get; set; } = string.Empty;

    [ForeignKey(nameof(CourseId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Course? Course { get; set; }

    public DateTime RegisteredDate { get; set; } = DateTime.Now;
}
