using WebAndMobile.Models;

namespace WebAndMobile.ViewModels;

public class TeacherProfileSetupViewModel
{
    public string? ProgramId { get; set; }
    public string? CourseId { get; set; }
    public List<AcademicProgram> Programs { get; set; } = new();
}
