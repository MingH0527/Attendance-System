using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class Teacher
{
    [Key]
    [MaxLength(20)]
    public string TeacherId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal? Salary { get; set; }

    public DateTime? HireDate { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
