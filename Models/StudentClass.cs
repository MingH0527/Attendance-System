using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(UserId), IsUnique = true)]
public class StudentClass
{
    [Key]
    [MaxLength(20)]
    public string StudentClassId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User? User { get; set; }

    [Required]
    [MaxLength(20)]
    public string StudentId { get; set; } = "";

    [MaxLength(20)]
    public string ProgramId { get; set; } = string.Empty;

    [ForeignKey(nameof(ProgramId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public AcademicProgram? Program { get; set; }

    [MaxLength(20)]
    public string ClassId { get; set; } = string.Empty;

    [ForeignKey(nameof(ClassId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ClassGroup? ClassGroup { get; set; }

    public DateTime RegisteredDate { get; set; } = DateTime.Now;
}
