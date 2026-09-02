using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[PrimaryKey(nameof(UserId), nameof(ProgramId))]
public class TeacherProgram
{
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User? User { get; set; }

    [MaxLength(20)]
    public string ProgramId { get; set; } = string.Empty;

    [ForeignKey(nameof(ProgramId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public AcademicProgram? Program { get; set; }

    public DateTime RegisteredDate { get; set; } = DateTime.Now;
}