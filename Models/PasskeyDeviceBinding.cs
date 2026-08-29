using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(BindingToken), IsUnique = true)]
[Index(nameof(DeviceIdentifier), nameof(Status))]
public class PasskeyDeviceBinding
{
    [Key]
    [MaxLength(20)]
    public string PasskeyDeviceBindingId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string BindingToken { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DeviceIdentifier { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; set; } = null!;
}
