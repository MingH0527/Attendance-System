using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class UserFaceProfile
{
    [Key]
    public int UserFaceProfileId { get; set; }

    public int UserId { get; set; }

    [Required]
    public string EmbeddingJson { get; set; } = "";

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastVerifiedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
}
