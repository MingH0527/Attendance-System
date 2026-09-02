using WebAndMobile.Models;

namespace WebAndMobile.ViewModels;

public class TeacherProfileSetupViewModel
{
    // Multiple programs
    public List<string> SelectedProgramIds { get; set; } = new();

    // Multiple subjects
    public List<string> SelectedCourseIds { get; set; } = new();

    // All available programs
    public List<AcademicProgram> Programs { get; set; } = new();

    // Subjects grouped under programs
    public List<TeacherSubjectOptionViewModel> Subjects { get; set; } = new();
}

public class TeacherSubjectOptionViewModel
{
    public string ProgramId { get; set; } = string.Empty;

    public string CourseId { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string? CourseCode { get; set; }
}