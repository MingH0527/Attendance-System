namespace WebAndMobile.Models;

public class UserListItemDto
{
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = "";

    public string Email { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Address { get; set; } = "";

    public string Gender { get; set; } = "";

    public string Position { get; set; } = "";

    public string Status { get; set; } = "";

    public string? Avatar { get; set; }

    // Program registration info
    // Used by both Student and Teacher
    public bool HasProgram { get; set; }

    public string? ProgramId { get; set; }

    public string? ProgramName { get; set; }

    // Student only
    public string? ClassId { get; set; }

    public string? ClassName { get; set; }

    public string? StudentId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}