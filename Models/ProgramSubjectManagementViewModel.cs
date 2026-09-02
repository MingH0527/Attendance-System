using WebAndMobile.Models;

namespace WebAndMobile.ViewModels;

public class ProgramSubjectManagementViewModel
{
    public List<AcademicProgram> Programs { get; set; } = new();
    public List<ProgramSubjectMappingItem> Mappings { get; set; } = new();
    public List<ProgramSubjectHistory> History { get; set; } = new();
    public string Mode { get; set; } = "management";
    public string HistorySort { get; set; } = "newest";
}

public class ProgramSubjectMappingItem
{
    public string ProgramCourseId { get; set; } = string.Empty;
    public string ProgramId { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? CourseCode { get; set; }
}
