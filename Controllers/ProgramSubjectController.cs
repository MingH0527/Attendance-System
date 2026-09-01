using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Admin")]
public class ProgramSubjectController : Controller
{
    private readonly DB _db;

    public ProgramSubjectController(DB db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string mode = "management", string sort = "newest")
    {
        mode = string.Equals(mode, "history", StringComparison.OrdinalIgnoreCase) ? "history" : "management";
        sort = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase) ? "oldest" : "newest";

        IQueryable<ProgramSubjectHistory> historyQuery = _db.ProgramSubjectHistories.AsNoTracking();
        historyQuery = sort == "oldest"
            ? historyQuery.OrderBy(h => h.CreatedAt)
            : historyQuery.OrderByDescending(h => h.CreatedAt);

        var model = new ProgramSubjectManagementViewModel
        {
            Mode = mode,
            HistorySort = sort,
            Programs = await _db.AcademicPrograms.AsNoTracking()
                .OrderBy(p => p.ProgramName)
                .ToListAsync(),
            Mappings = await _db.ProgramCourses.AsNoTracking()
                .Include(pc => pc.Course)
                .OrderBy(pc => pc.Course!.CourseName)
                .Select(pc => new ProgramSubjectMappingItem
                {
                    ProgramCourseId = pc.ProgramCourseId,
                    ProgramId = pc.ProgramId,
                    CourseId = pc.CourseId,
                    CourseName = pc.Course!.CourseName,
                    CourseCode = pc.Course.CourseCode
                }).ToListAsync(),
            History = await historyQuery.Take(500).ToListAsync()
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProgram(string programName)
    {
        programName = NormalizeName(programName);
        if (programName.Length < 3 || programName.Length > 150)
            return Fail("Program name must be between 3 and 150 characters.");

        if (await _db.AcademicPrograms.AnyAsync(p => p.ProgramName == programName))
            return Fail("A program with the same name already exists.");

        string programId = await FormattedIdGenerator.NextAsync(
            _db.AcademicPrograms, p => p.ProgramId, "PRG");

        var program = new AcademicProgram
        {
            ProgramId = programId,
            // ProgramCode is kept for compatibility with existing timetable/UI code.
            // New records use the generated database ID so Admin never has to type another code.
            ProgramCode = programId,
            ProgramName = programName
        };

        _db.AcademicPrograms.Add(program);
        await AddHistory("Created", "Program", program.ProgramId, null,
            $"Created program {program.ProgramName} ({program.ProgramId}).");
        await _db.SaveChangesAsync();

        return Success($"Program {program.ProgramName} created.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgram(string programId, string programName)
    {
        var program = await _db.AcademicPrograms.FindAsync(programId);
        if (program is null) return Fail("Program not found.");

        programName = NormalizeName(programName);
        if (programName.Length < 3 || programName.Length > 150)
            return Fail("Program name must be between 3 and 150 characters.");

        if (await _db.AcademicPrograms.AnyAsync(p => p.ProgramId != programId && p.ProgramName == programName))
            return Fail("A program with the same name already exists.");

        string oldName = program.ProgramName;
        program.ProgramName = programName;

        await AddHistory("Updated", "Program", program.ProgramId, null,
            $"Renamed program from {oldName} to {programName} ({program.ProgramId}).");
        await _db.SaveChangesAsync();

        return Success("Program updated.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProgram(string programId)
    {
        var program = await _db.AcademicPrograms.FindAsync(programId);
        if (program is null) return Fail("Program not found.");

        if (await _db.ClassGroups.AnyAsync(c => c.ProgramId == programId) ||
            await _db.StudentClasses.AnyAsync(s => s.ProgramId == programId))
        {
            return Fail("This program is already used by classes or students, so it cannot be deleted.");
        }

        var mappings = await _db.ProgramCourses.Where(x => x.ProgramId == programId).ToListAsync();
        var courseIds = mappings.Select(x => x.CourseId).Distinct().ToList();

        await AddHistory("Deleted", "Program", program.ProgramId, null,
            $"Deleted program {program.ProgramName} ({program.ProgramId}).");

        _db.ProgramCourses.RemoveRange(mappings);
        _db.AcademicPrograms.Remove(program);
        await _db.SaveChangesAsync();
        await RemoveOrphanCourses(courseIds);

        return Success("Program deleted.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubject(string programId, string courseName)
    {
        var program = await _db.AcademicPrograms.FindAsync(programId);
        if (program is null) return Fail("Please select a valid program.");

        courseName = NormalizeName(courseName);
        if (courseName.Length < 2 || courseName.Length > 150)
            return Fail("Subject name must be between 2 and 150 characters.");

        Course? subject = await _db.Courses.FirstOrDefaultAsync(c => c.CourseName == courseName);

        if (subject == null)
        {
            string courseId = await FormattedIdGenerator.NextAsync(_db.Courses, c => c.CourseId, "CRS");
            subject = new Course
            {
                CourseId = courseId,
                CourseCode = courseId,
                CourseName = courseName
            };
            _db.Courses.Add(subject);
        }

        if (await _db.ProgramCourses.AnyAsync(pc => pc.ProgramId == programId && pc.CourseId == subject.CourseId))
            return Fail($"{subject.CourseName} is already registered under {program.ProgramName}.");

        var mapping = new ProgramCourse
        {
            ProgramCourseId = await FormattedIdGenerator.NextAsync(
                _db.ProgramCourses, x => x.ProgramCourseId, "PCR"),
            ProgramId = programId,
            CourseId = subject.CourseId
        };

        _db.ProgramCourses.Add(mapping);
        await AddHistory("Added", "Subject", program.ProgramId, subject.CourseId,
            $"Added subject {subject.CourseName} ({subject.CourseId}) to {program.ProgramName}.");
        await _db.SaveChangesAsync();

        return Success($"{subject.CourseName} added to {program.ProgramName}.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgramSubject(
        string programId,
        string programCourseId,
        string courseId,
        string courseName)
    {
        var mapping = await _db.ProgramCourses
            .FirstOrDefaultAsync(x => x.ProgramCourseId == programCourseId && x.ProgramId == programId);
        var subject = await _db.Courses.FindAsync(courseId);
        var program = await _db.AcademicPrograms.FindAsync(programId);

        if (mapping is null || subject is null || program is null)
            return Fail("Program subject not found.");

        courseName = NormalizeName(courseName);
        if (courseName.Length < 2 || courseName.Length > 150)
            return Fail("Subject name must be between 2 and 150 characters.");

        if (await _db.Courses.AnyAsync(c => c.CourseId != courseId && c.CourseName == courseName))
            return Fail("Another subject already uses that name.");

        string oldName = subject.CourseName;
        subject.CourseName = courseName;

        await AddHistory("Updated", "Subject", programId, courseId,
            $"Renamed subject from {oldName} to {courseName} ({courseId}) in {program.ProgramName}.");
        await _db.SaveChangesAsync();

        return Success("Subject updated. Existing timetable rows will show the new subject name immediately.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProgramSubject(string programId, string programCourseId)
    {
        var mapping = await _db.ProgramCourses
            .Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.ProgramCourseId == programCourseId && x.ProgramId == programId);
        var program = await _db.AcademicPrograms.FindAsync(programId);

        if (mapping is null || program is null)
            return Fail("Program subject not found.");

        var classIds = await _db.ClassGroups
            .Where(c => c.ProgramId == mapping.ProgramId)
            .Select(c => c.ClassId)
            .ToListAsync();

        if (await _db.Timetables.AnyAsync(t => classIds.Contains(t.ClassId) && t.CourseId == mapping.CourseId))
            return Fail("This subject is already used in this program's timetable. Remove those timetable lessons first.");

        string courseId = mapping.CourseId;
        string subjectName = mapping.Course?.CourseName ?? courseId;

        await AddHistory("Removed", "Subject", programId, courseId,
            $"Removed subject {subjectName} ({courseId}) from {program.ProgramName}.");

        _db.ProgramCourses.Remove(mapping);
        await _db.SaveChangesAsync();
        await RemoveOrphanCourses(new[] { courseId });

        return Success("Subject removed from program.");
    }

    private async Task AddHistory(
        string action,
        string entityType,
        string? programId,
        string? courseId,
        string description)
    {
        _db.ProgramSubjectHistories.Add(new ProgramSubjectHistory
        {
            HistoryId = await FormattedIdGenerator.NextAsync(
                _db.ProgramSubjectHistories, h => h.HistoryId, "PSH"),
            Action = action,
            EntityType = entityType,
            ProgramId = programId,
            CourseId = courseId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task RemoveOrphanCourses(IEnumerable<string> courseIds)
    {
        foreach (var courseId in courseIds.Distinct())
        {
            bool stillUsed = await _db.ProgramCourses.AnyAsync(x => x.CourseId == courseId)
                || await _db.TeacherCourses.AnyAsync(x => x.CourseId == courseId)
                || await _db.Timetables.AnyAsync(x => x.CourseId == courseId);

            if (!stillUsed)
            {
                var course = await _db.Courses.FindAsync(courseId);
                if (course != null) _db.Courses.Remove(course);
            }
        }

        await _db.SaveChangesAsync();
    }

    private static string NormalizeName(string? value) =>
        Regex.Replace((value ?? string.Empty).Trim(), "\\s+", " ");

    private IActionResult Success(string message)
    {
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index), new { mode = "management" });
    }

    private IActionResult Fail(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index), new { mode = "management" });
    }
}
