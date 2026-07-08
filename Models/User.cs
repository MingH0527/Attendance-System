using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class User
{
    [Key]
    public int UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = "";

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = "";

    [MaxLength(20)]
    public string Phone { get; set; } = "";

    [MaxLength(200)]
    public string Address { get; set; } = "";

    [MaxLength(10)]
    public string Gender { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string Position { get; set; } = "Student";

    [MaxLength(255)]
    public string Avatar { get; set; } = "default.png";

    [MaxLength(20)]
    public string Status { get; set; } = "Active";
}