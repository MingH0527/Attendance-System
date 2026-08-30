using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(CredentialId), IsUnique = true)]
[Index(nameof(DeviceIdentifier))]
[Index(nameof(UserId), nameof(DeviceIdentifier), IsUnique = true)]
public class UserPasskey
{
    [Key]
    [MaxLength(20)]
    public string UserPasskeyId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public byte[] CredentialId { get; set; } = [];

    [Required]
    public byte[] PublicKey { get; set; } = [];

    public uint SignCount { get; set; }
    public Guid AaGuid { get; set; }

    [MaxLength(100)]
    public string DeviceName { get; set; } = "Passkey Device";

    public byte[]? UserHandle { get; set; }

    [MaxLength(50)]
    public string CredentialType { get; set; } = "public-key";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; set; } = null!;

    [MaxLength(100)]
    public string? DeviceIdentifier { get; set; }

    [MaxLength(255)]
    public string? RelyingPartyId { get; set; }

    [MaxLength(100)]
    public string? DeviceType { get; set; }

    [MaxLength(100)]
    public string? OperatingSystem { get; set; }

    [MaxLength(100)]
    public string? Browser { get; set; }

    [MaxLength(50)]
    public string? AuthenticatorAttachment { get; set; }

    [MaxLength(200)]
    public string? AuthenticatorTransports { get; set; }

    [MaxLength(300)]
    public string? UserAgent { get; set; }
}
