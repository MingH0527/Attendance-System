using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Models;

public class TimetableException
{
    [Key, MaxLength(20)] public string ExceptionId { get; set; } = string.Empty;
    [MaxLength(20)] public string TimetableId { get; set; } = string.Empty;
    [ForeignKey(nameof(TimetableId)), DeleteBehavior(DeleteBehavior.Cascade)] public Timetable? Timetable { get; set; }
    public DateTime OccurrenceDate { get; set; }
    [MaxLength(30)] public string Reason { get; set; } = "Other";
    [MaxLength(30)] public string Decision { get; set; } = "Pending";
    [MaxLength(250)] public string? Note { get; set; }

    // Specific-date override. These values never alter the weekly Timetable template.
    [MaxLength(20)] public string? OverrideClassroomId { get; set; }
    public TimeSpan? OverrideStartTime { get; set; }
    public TimeSpan? OverrideEndTime { get; set; }
    [MaxLength(20)] public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
