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


// ==================================================
// ATTENDANCE CODE
// ==================================================

public class TeacherAttendanceCodeViewModel
{
    // Currently selected attendance date
    public DateTime Date { get; set; }

    public DateTime SelectedDate { get; set; }

    // All dates that can be selected.
    // Newest date first.
    public List<DateTime> AvailableDates { get; set; } =
        new();

    // Only classes belonging to SelectedDate
    public List<TeacherAttendanceCodeItemViewModel> Classes { get; set; } =
        new();

    // Total number of class occurrences
    // from semester start until today.
    public int TotalItems { get; set; }

    // ==================================================
    // OLD PAGINATION PROPERTIES
    //
    // Keep them so nothing else breaks.
    // They are no longer used by AttendanceCode view.
    // ==================================================

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 5;

    public int TotalPages { get; set; }
}


public class TeacherAttendanceCodeItemViewModel
{
    public string TimetableId { get; set; } =
        string.Empty;

    public string? ReplacementId { get; set; }

    // Each occurrence has its own actual date
    public DateTime ClassDate { get; set; }

    public string CourseName { get; set; } =
        string.Empty;

    public string ClassName { get; set; } =
        string.Empty;

    public string RoomName { get; set; } =
        string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    // Normal / Replacement
    public string Status { get; set; } =
        "Normal";

    public bool CanGenerate { get; set; }

    public string? DisabledReason { get; set; }

    public string? ActiveCode { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool HasGeneratedCode { get; set; }

    public bool CanViewStudents { get; set; }

    public bool NeedsManualAttendance { get; set; }
}


// ==================================================
// MANAGE ATTENDANCE
// ==================================================

public class TeacherManageAttendanceViewModel
{
    public string TimetableId { get; set; } =
        string.Empty;

    public string? ReplacementId { get; set; }

    public DateTime Date { get; set; }

    public string CourseName { get; set; } =
        string.Empty;

    public string ClassName { get; set; } =
        string.Empty;

    public string RoomName { get; set; } =
        string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool ClassEnded { get; set; }

    public bool HasGeneratedCode { get; set; }

    public List<TeacherManageAttendanceStudentViewModel> Students { get; set; } =
        new();
}


public class TeacherManageAttendanceStudentViewModel
{
    public string StudentClassId { get; set; } =
        string.Empty;

    public string UserId { get; set; } =
        string.Empty;

    public string StudentId { get; set; } =
        string.Empty;

    public string Username { get; set; } =
        string.Empty;

    // Present / Absent / Leave
    public string Status { get; set; } =
        "Absent";

    public string? Description { get; set; }

    public bool CanEdit { get; set; }
}