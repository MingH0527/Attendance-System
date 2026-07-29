using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class PaymentSecurity
{
    [Key]
    public int PaymentSecurityId { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string PinHash { get; set; } = "";

    public bool PinEnabled { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
