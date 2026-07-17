using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class AdminSecurity
{
    [Key]
    public int AdminSecurityId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string PinHash { get; set; } = "";

    public bool PinEnabled { get; set; } = false;

    public int FailedAttempts { get; set; } = 0;

    public DateTime? LockoutEnd { get; set; }

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}