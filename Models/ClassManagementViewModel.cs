namespace WebAndMobile.ViewModels;

public class ClassManagementViewModel
{
    /// Maximum classes allowed per program.
    public const int MaxClassesPerProgram = 2;

    public List<ProgramClassRow> Programs { get; set; } = [];

    /// "asc" or "desc" - applied to Program ID.
    public string Sort { get; set; } = "asc";

    public string? Search { get; set; }

    public int TotalPrograms { get; set; }

    public int TotalClasses { get; set; }

    /// Programs with no class yet: these cannot be timetabled.
    public int ProgramsWithoutClass { get; set; }
}

public class ProgramClassRow
{
    public string ProgramId { get; set; } = "";

    public string ProgramName { get; set; } = "";

    public List<ClassRow> Classes { get; set; } = [];

    public bool IsFull =>
        Classes.Count >= ClassManagementViewModel.MaxClassesPerProgram;

    public bool HasNoClass => Classes.Count == 0;
}

public class ClassRow
{
    public string ClassId { get; set; } = "";

    public string ClassName { get; set; } = "";

    /// Students currently registered to this class.
    public int StudentCount { get; set; }

    /// Timetable entries referencing this class.
    public int TimetableCount { get; set; }
}