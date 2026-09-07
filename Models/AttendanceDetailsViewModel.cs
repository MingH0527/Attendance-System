namespace WebAndMobile.Models;

public class AttendanceDetailsViewModel
{
    public string CourseId { get; set; } = "";

    public string CourseCode { get; set; } = "";

    public string CourseName { get; set; } = "";

    public int PresentCount { get; set; }

    public int AbsentCount { get; set; }

    public int LeaveCount { get; set; }

    public double Percentage { get; set; }

    public double PresentPercent { get; set; }

    public double AbsentPercent { get; set; }

    public double LeavePercent { get; set; }

    public List<AttendanceHistoryItemViewModel> History { get; set; }
        = new();
}

public class AttendanceHistoryItemViewModel
{
    public DateTime Date { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Status { get; set; } = "";

    public string? Description { get; set; }
}