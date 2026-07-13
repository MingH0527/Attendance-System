using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class Classroom
{
    [Key]
    public int ClassroomId { get; set; }

    [Required]
    [MaxLength(100)]
    public string RoomName { get; set; } = string.Empty;

    public ICollection<Timetable> Timetables { get; set; }
        = new List<Timetable>();
}