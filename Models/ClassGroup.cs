using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Table("Classes")]
[Index(nameof(ClassName), IsUnique = true)]
public class ClassGroup
{
    [Key]
    [MaxLength(20)]
    public string ClassId { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string ClassName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ProgramId { get; set; } = string.Empty;

    [ForeignKey(nameof(ProgramId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AcademicProgram? Program { get; set; }

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}
