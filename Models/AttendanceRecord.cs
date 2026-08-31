using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(
    nameof(AttendanceSessionId),
    nameof(StudentClassId),
    IsUnique = true
)]
public class AttendanceRecord
{
    [Key]
    [MaxLength(20)]
    public string AttendanceRecordId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string AttendanceSessionId { get; set; } = string.Empty;

    [ForeignKey(nameof(AttendanceSessionId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AttendanceSession? AttendanceSession { get; set; }

    [Required]
    [MaxLength(20)]
    public string StudentClassId { get; set; } = string.Empty;

    [ForeignKey(nameof(StudentClassId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public StudentClass? StudentClass { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Present";

    public DateTime SubmittedAt { get; set; }

    // Teacher removes fake / invalid attendance
    public bool IsRemoved { get; set; } = false;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime? RemovedAt { get; set; }
}