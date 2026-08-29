using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(UserId), IsUnique = true)]
public class PaymentSecurity
{
    [Key]
    [MaxLength(20)]
    public string PaymentSecurityId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PinHash { get; set; } = "";

    public bool PinEnabled { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; set; } = null!;
}
