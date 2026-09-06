using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class TimetableAudit
{
    [Key, MaxLength(20)]
    public string AuditId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? TermId { get; set; }

    [MaxLength(20)]
    public string TimetableId { get; set; } = string.Empty;

    public DateTime OccurrenceDate { get; set; }

    [Required, MaxLength(60)]
    public string IssueType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(500)]
    public string? BeforeValue { get; set; }

    [MaxLength(500)]
    public string? AfterValue { get; set; }

    [MaxLength(250)]
    public string? Reason { get; set; }

    [MaxLength(20)]
    public string? ChangedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ResolvedAt { get; set; }
}
