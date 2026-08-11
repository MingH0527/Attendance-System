using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class PasskeyDeviceBinding
{
    [Key]
    public int PasskeyDeviceBindingId { get; set; }

    [Required]
    [MaxLength(64)]
    public string BindingToken { get; set; } = "";

    [Required]
    public int UserId { get; set; }

    [MaxLength(100)]
    public string? DeviceIdentifier { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
