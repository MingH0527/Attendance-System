using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

[Index(nameof(RoomName), IsUnique = true)]
public class Classroom
{
    [Key]
    [MaxLength(20)]
    public string ClassroomId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RoomName { get; set; } = string.Empty;

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}
