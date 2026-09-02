using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Models;

[Index(nameof(CreatedAt))]
public class ProgramSubjectHistory
{
    [Key, MaxLength(20)]
    public string HistoryId { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ProgramId { get; set; }

    [MaxLength(20)]
    public string? CourseId { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
