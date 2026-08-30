using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Models;

[Index(nameof(ProgramId), nameof(CourseId), IsUnique = true)]
public class ProgramCourse
{
    [Key, MaxLength(20)]
    public string ProgramCourseId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ProgramId { get; set; } = string.Empty;

    [ForeignKey(nameof(ProgramId)), DeleteBehavior(DeleteBehavior.Cascade)]
    public AcademicProgram? Program { get; set; }

    [MaxLength(20)]
    public string CourseId { get; set; } = string.Empty;

    [ForeignKey(nameof(CourseId)), DeleteBehavior(DeleteBehavior.Cascade)]
    public Course? Course { get; set; }

}