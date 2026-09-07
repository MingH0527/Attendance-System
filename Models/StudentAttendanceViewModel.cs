namespace WebAndMobile.Models;

public class StudentAttendanceViewModel
{
    public string Code { get; set; } = "";

    public List<TodayAttendanceItemViewModel> TodayAttendance { get; set; }
        = new();

    public List<SubjectAttendanceViewModel> Subjects { get; set; }
        = new();
}

public class TodayAttendanceItemViewModel
{
    public string CourseId { get; set; } = "";

    public string CourseCode { get; set; } = "";

    public string CourseName { get; set; } = "";

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Status { get; set; } = "";
}

public class SubjectAttendanceViewModel
{
    public string CourseId { get; set; } = "";

    public string CourseCode { get; set; } = "";

    public string CourseName { get; set; } = "";

    public int PresentCount { get; set; }

    public int AbsentCount { get; set; }

    public int LeaveCount { get; set; }

    public double Percentage { get; set; }
}