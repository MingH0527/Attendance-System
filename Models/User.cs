using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class User
{
    [Key]
    public int UserId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Username { get; set; } = "";

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = "";

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = "";

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [Required]
    [MaxLength(10)]
    public string Gender { get; set; } = "";

    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = "";

    [NotMapped]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string Position { get; set; } = "Student";

    [MaxLength(255)]
    public string Avatar { get; set; } = "default.png";

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedDate { get; set; } =
        DateTime.UtcNow;

    // OTP verification fields

    public bool IsEmailVerified { get; set; } =
        false;

    [MaxLength(500)]
    public string? OtpHash { get; set; }

    public DateTime? OtpExpiresAt { get; set; }

    public DateTime? OtpLastSentAt { get; set; }

    public int OtpAttemptCount { get; set; } =
        0;
}