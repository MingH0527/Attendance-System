using System.ComponentModel.DataAnnotations;
using WebAndMobile.Models;

namespace WebAndMobile.ViewModels;

// Sent from the client when creating or editing a timetable entry.
public class TimetableRequest
{
    public string? TermId { get; set; }
    // Only present when editing an existing entry.
    public string? TimetableId { get; set; }

    [Required(ErrorMessage = "Please select a teacher.")]
    public string TeacherId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a course.")]
    public string CourseId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a class.")]
    public string ClassId { get; set; } = string.Empty;

    public string? ClassroomId { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    // Sent as "HH:mm" strings from the <input type="time"> fields.
    [Required(ErrorMessage = "Please provide a start time.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please provide an end time.")]
    public string EndTime { get; set; } = string.Empty;

    [Required]
    public ClassType ClassType { get; set; }

    // Weekly-template creation/editing is always Normal. A specific Week is required for Cancelled.
    public string Status { get; set; } = "Normal";
}

// Returned to the client to render each populated cell in the grid.
public class TimetableSlotDto
{
    public string? TermId { get; set; }
    public string Status { get; set; } = "Normal";
    public string? StatusReason { get; set; }
    public bool IsUpdated { get; set; }
    public bool IsReplacement { get; set; }
    public bool HasReplacement { get; set; }
    public string? ReplacementId { get; set; }
    public DateTime? SourceOccurrenceDate { get; set; }
    public string TimetableId { get; set; } = string.Empty;
    public string TeacherId { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? ClassroomId { get; set; }
    public string? RoomName { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty; // "HH:mm"
    public string EndTime { get; set; } = string.Empty;   // "HH:mm"
    public ClassType ClassType { get; set; }
}

public class TeacherLookupDto
{
    public string TeacherId { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
}

public class SimpleLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}

public class UserRoleDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}

public class CreateAcademicTermRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool ForceActivate { get; set; }
}

public class UpdateAcademicTermDatesRequest
{
    public string TermId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TimetableDashboardDto { public List<TermDashboardDto> Current {get;set;}=new(); public List<TermDashboardDto> Completed {get;set;}=new(); }
public class TermDashboardDto { public string TermId {get;set;}=string.Empty; public string Name {get;set;}=string.Empty; public DateTime StartDate {get;set;} public DateTime EndDate {get;set;} public string Status {get;set;}=string.Empty; public bool IsRestored {get;set;} public List<ProgramDashboardDto> Programs {get;set;}=new(); }
public class ProgramDashboardDto { public string ProgramId {get;set;}=string.Empty; public string ProgramCode {get;set;}=string.Empty; public string ProgramName {get;set;}=string.Empty; public List<ClassDashboardDto> Classes {get;set;}=new(); }
public class ClassDashboardDto { public string ClassId {get;set;}=string.Empty; public string ClassName {get;set;}=string.Empty; }
public class TeacherCourseLookupDto { public string TeacherId {get;set;}=string.Empty; public string TeacherName {get;set;}=string.Empty; public string CourseId {get;set;}=string.Empty; public string CourseName {get;set;}=string.Empty; }
public class HolidayDecisionRequest { public string TimetableId {get;set;}=string.Empty; public DateTime Date {get;set;} public string Decision {get;set;}=string.Empty; public string? Note {get;set;} }
public class ReplacementRequest { public string SourceTimetableId {get;set;}=string.Empty; public DateTime SourceOccurrenceDate {get;set;} public DateTime Date {get;set;} public string StartTime {get;set;}=string.Empty; public string EndTime {get;set;}=string.Empty; public string? ClassroomId {get;set;} }

public class ReplacementRequiredDto
{
    public string TimetableId { get; set; } = string.Empty;
    public DateTime OccurrenceDate { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherId { get; set; } = string.Empty;
    public string? ClassroomId { get; set; }
    public string? RoomName { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool HasReplacement { get; set; }
    public DateTime? ReplacementDate { get; set; }
    public string? ReplacementStartTime { get; set; }
    public string? ReplacementEndTime { get; set; }
    public string? ReplacementRoomName { get; set; }
}


public class OccurrenceStatusRequest { public string TimetableId {get;set;}=string.Empty; public DateTime Date {get;set;} public string Status {get;set;}="Normal"; public string? Note {get;set;} }

public class OccurrenceUpdateRequest
{
    public string TimetableId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? ClassroomId { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal";
    public string? Note { get; set; }
}

public class RoomAvailabilityDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string? Reason { get; set; }
}
