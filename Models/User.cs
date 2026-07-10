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

    // GOAL 3 from the PHP register page: address is picked on a map,
    // so we persist the coordinates alongside the free-text address.
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [Required]
    [MaxLength(10)]
    public string Gender { get; set; } = "";

    // Stores the HASHED password (via IPasswordHasher<User>), never plaintext.
    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = "";

    // Only used for form validation - never saved to the database.
    [NotMapped]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string Position { get; set; } = "Student";

    [MaxLength(255)]
    public string Avatar { get; set; } = "default.png";

    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}