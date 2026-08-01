using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Table("Classes")]
public class ClassGroup
{
    [Key]
    public int ClassId { get; set; }

    [Required]
    [MaxLength(150)]
    public string ClassName { get; set; } = string.Empty;

    [ForeignKey(nameof(Program))]
    public int ProgramId { get; set; }
    public AcademicProgram? Program { get; set; }

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}