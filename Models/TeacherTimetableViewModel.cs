namespace WebAndMobile.Models;

public class TeacherTimetableViewModel
{
    public string TermName { get; set; } =
        string.Empty;

    public bool IsTemplate { get; set; }

    public int? SelectedWeekNumber { get; set; }

    public DateTime? WeekStart { get; set; }

    public DateTime? WeekEnd { get; set; }

    public List<AcademicWeek> Weeks { get; set; } =
        new();

    public List<TeacherTimetableItemViewModel> Items { get; set; } =
        new();

    public List<TimetableNotification> Notifications { get; set; } =
        new();
}


public class TeacherTimetableItemViewModel
{
    public string TimetableId { get; set; } =
        string.Empty;

    public string CourseId { get; set; } =
        string.Empty;

    public string CourseName { get; set; } =
        string.Empty;

    public string ClassName { get; set; } =
        string.Empty;

    public string RoomName { get; set; } =
        string.Empty;

    public DayOfWeek DayOfWeek { get; set; }

    public DateTime? OccurrenceDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public ClassType ClassType { get; set; }

    // Normal / Replacement / Cancelled
    public string Status { get; set; } =
        "Normal";

    public string? ReplacementId { get; set; }

    public string? Reason { get; set; }
}


public class TeacherAttendanceCodeViewModel
{
    public DateTime Date { get; set; }

    public List<TeacherAttendanceCodeItemViewModel>
        Classes
    { get; set; } =
            new();
}


public class TeacherAttendanceCodeItemViewModel
{
    public string TimetableId { get; set; } =
        string.Empty;

    public string? ReplacementId { get; set; }

    public string CourseName { get; set; } =
        string.Empty;

    public string ClassName { get; set; } =
        string.Empty;

    public string RoomName { get; set; } =
        string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Status { get; set; } =
        "Normal";

    public bool CanGenerate { get; set; }

    public string? DisabledReason { get; set; }

    public string? ActiveCode { get; set; }

    public DateTime? ExpiresAt { get; set; }
}