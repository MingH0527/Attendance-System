using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class PaymentTransaction
{
    [Key]
    public int PaymentTransactionId { get; set; }

    public int UserId { get; set; }
    public int FeeId { get; set; }

    [Required, MaxLength(100)]
    public string FeeName { get; set; } = "";

    [Range(0.01, 100000)]
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

    public User User { get; set; } = null!;
}
