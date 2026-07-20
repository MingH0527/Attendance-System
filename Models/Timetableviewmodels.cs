using System.ComponentModel.DataAnnotations;
using WebAndMobile.Models;

namespace WebAndMobile.ViewModels;

// Sent from the client when creating or editing a timetable entry.
public class TimetableRequest
{
    // Only present when editing an existing entry.
    public int? TimetableId { get; set; }

    [Required(ErrorMessage = "Please select a teacher.")]
    public int TeacherId { get; set; }

    [Required(ErrorMessage = "Please select a course.")]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Please select a class.")]
    public int ClassId { get; set; }

    public int? ClassroomId { get; set; }

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
    public int TimetableId { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public string? RoomName { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty; // "HH:mm"
    public string EndTime { get; set; } = string.Empty;   // "HH:mm"
    public ClassType ClassType { get; set; }
}

public class TeacherLookupDto
{
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
}

public class SimpleLookupDto
{
    public int Id { get; set; }
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
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}