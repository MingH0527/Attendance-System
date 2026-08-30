using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

[Index(nameof(UserId), IsUnique = true)]
public class UserFaceProfile
{
    [Key]
    [MaxLength(20)]
    public string UserFaceProfileId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string EmbeddingJson { get; set; } = "";

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastVerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; set; } = null!;
}
