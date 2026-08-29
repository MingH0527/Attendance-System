using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(QrToken), IsUnique = true)]
public class PaymentTransaction
{
    [Key]
    [MaxLength(20)]
    public string PaymentTransactionId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public int FeeId { get; set; }

    [Required, MaxLength(100)]
    public string FeeName { get; set; } = "";

    [Range(0.01, 100000)]
    [Precision(18, 2)]
    public decimal Amount { get; set; }

    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = "";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending";

    [Required, MaxLength(100)]
    public string QrToken { get; set; } = "";

    public DateTime QrExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; set; } = null!;
}
