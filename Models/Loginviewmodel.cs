using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(30)]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}