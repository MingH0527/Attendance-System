using System.ComponentModel.DataAnnotations;
using WebAndMobile.Models;

namespace WebAndMobile.ViewModels;

// Sent from the client when creating or editing a timetable entry.
public class TimetableRequest
{
    // Only present when editing an existing entry.
    public string? TimetableId { get; set; }

    [Required(ErrorMessage = "Please select a teacher.")]
    public string TeacherId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a course.")]
    public string CourseId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a class.")]
    public string ClassId { get; set; } = string.Empty;

    public string? ClassroomId { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    // Sent as "HH:mm" strings from the <input type="time"> fields.
    [Required(ErrorMessage = "Please provide a start time.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please provide an end time.")]
    public string EndTime { get; set; } = string.Empty;

    [Required]
    public ClassType ClassType { get; set; }
}

// Returned to the client to render each populated cell in the grid.
public class TimetableSlotDto
{
    public string TimetableId { get; set; } = string.Empty;
    public string TeacherId { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? ClassroomId { get; set; }
    public string? RoomName { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty; // "HH:mm"
    public string EndTime { get; set; } = string.Empty;   // "HH:mm"
    public ClassType ClassType { get; set; }
}

public class TeacherLookupDto
{
    public string TeacherId { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
}

public class SimpleLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}

public class UserRoleDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}