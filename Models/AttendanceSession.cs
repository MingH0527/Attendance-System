using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(TimetableId), nameof(SessionDate), IsUnique = true)]
[Index(nameof(Code))]
public class AttendanceSession
{
    [Key]
    [MaxLength(20)]
    public string AttendanceSessionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string TimetableId { get; set; } = string.Empty;

    [ForeignKey(nameof(TimetableId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Timetable? Timetable { get; set; }

    // Which date this attendance belongs to
    public DateTime SessionDate { get; set; }

    [Required]
    [MaxLength(6)]
    [MinLength(6)]
    public string Code { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
        = new List<AttendanceRecord>();
}