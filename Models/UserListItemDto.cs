namespace WebAndMobile.Models;

public class UserListItemDto
{
    public int UserId { get; set; }

    public string Username { get; set; } = "";

    public string Email { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Gender { get; set; } = "";

    public string Position { get; set; } = "";

    public string Status { get; set; } = "";

    public string? Avatar { get; set; }
}