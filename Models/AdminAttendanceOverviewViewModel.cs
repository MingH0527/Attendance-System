namespace WebAndMobile.Models;

public class AdminAttendanceOverviewViewModel
{
    public string TermName { get; set; } =
        string.Empty;

    public int TotalStudents { get; set; }

    public int TotalPresent { get; set; }

    public int TotalAbsent { get; set; }

    public int TotalLeave { get; set; }

    public double OverallAttendanceRate { get; set; }

    public List<AdminAttendanceSubjectViewModel>
        Subjects
    { get; set; } =
        new();

    public List<AdminAttendanceSubjectViewModel>
        TopAbsentSubjects
    { get; set; } =
        new();

    public List<AdminAttendanceDayViewModel>
        AbsenceByDay
    { get; set; } =
        new();
}


public class AdminAttendanceSubjectViewModel
{
    public string CourseId { get; set; } =
        string.Empty;

    public string CourseCode { get; set; } =
        string.Empty;

    public string CourseName { get; set; } =
        string.Empty;

    public int StudentCount { get; set; }

    public int PresentCount { get; set; }

    public int AbsentCount { get; set; }

    public int LeaveCount { get; set; }

    public double AttendanceRate { get; set; }
}


public class AdminAttendanceDayViewModel
{
    public string DayName { get; set; } =
        string.Empty;

    public int AbsentCount { get; set; }
}