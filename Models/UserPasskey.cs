using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class UserPasskey
{
    [Key]
    public int UserPasskeyId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public byte[] CredentialId { get; set; } = [];

    [Required]
    public byte[] PublicKey { get; set; } = [];

    public uint SignCount { get; set; }

    public Guid AaGuid { get; set; }

    [MaxLength(100)]
    public string DeviceName { get; set; } =
        "Passkey Device";

    public byte[]? UserHandle { get; set; }

    [MaxLength(50)]
    public string CredentialType { get; set; } =
        "public-key";

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}