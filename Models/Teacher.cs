using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class Teacher
{
    [Key]
    public int TeacherId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(10)]
    public string TeacherCode { get; set; } = "";

    public decimal? Salary { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Program { get; set; }

    public string? Subject { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}