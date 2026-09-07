using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(
    nameof(UserId),
    nameof(CourseId),
    IsUnique = true
)]
public class BarredStudent
{
    [Key]
    [MaxLength(20)]
    public string BarredStudentId { get; set; } =
        string.Empty;

    [Required]
    [MaxLength(20)]
    public string UserId { get; set; } =
        string.Empty;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User? Student { get; set; }

    [Required]
    [MaxLength(20)]
    public string CourseId { get; set; } =
        string.Empty;

    [ForeignKey(nameof(CourseId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Course? Course { get; set; }

    [Required]
    [MaxLength(300)]
    public string Reason { get; set; } =
        string.Empty;

    public DateTime BarredAt { get; set; } =
        DateTime.Now;
}