namespace WebAndMobile.Models;

public class BarredListViewModel
{
    public List<BarredStudentRowViewModel> BarredStudents { get; set; }
        = new();

    public List<BarredCandidateViewModel> EligibleStudents { get; set; }
        = new();
}


public class BarredCandidateViewModel
{
    public string UserId { get; set; } =
        string.Empty;

    public string StudentId { get; set; } =
        string.Empty;

    public string Username { get; set; } =
        string.Empty;

    public string ProgramName { get; set; } =
        string.Empty;

    public string CourseId { get; set; } =
        string.Empty;

    public string CourseCode { get; set; } =
        string.Empty;

    public string CourseName { get; set; } =
        string.Empty;

    public int AbsentCount { get; set; }

    public double AttendancePercentage { get; set; }
}


public class BarredStudentRowViewModel
{
    public string BarredStudentId { get; set; } =
        string.Empty;

    public string UserId { get; set; } =
        string.Empty;

    public string StudentId { get; set; } =
        string.Empty;

    public string Username { get; set; } =
        string.Empty;

    public string ProgramName { get; set; } =
        string.Empty;

    public string CourseId { get; set; } =
        string.Empty;

    public string CourseCode { get; set; } =
        string.Empty;

    public string CourseName { get; set; } =
        string.Empty;

    public int AbsentCount { get; set; }

    public double AttendancePercentage { get; set; }

    public string Reason { get; set; } =
        string.Empty;

    public DateTime BarredAt { get; set; }
}