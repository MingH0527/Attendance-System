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

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}