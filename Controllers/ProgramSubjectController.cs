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
    private static readonly Regex ProgramCodePattern = new("^[A-Z0-9-]{2,20}$", RegexOptions.Compiled);
    private static readonly Regex SubjectCodePattern = new("^[A-Z0-9-]{2,30}$", RegexOptions.Compiled);

    public ProgramSubjectController(DB db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? programId = null)
    {
        var programs = await _db.AcademicPrograms.AsNoTracking().OrderBy(p => p.ProgramCode).ToListAsync();
        if (!string.IsNullOrWhiteSpace(programId) && programs.All(p => p.ProgramId != programId))
            programId = null;

        var model = new ProgramSubjectManagementViewModel
        {
            Programs = programs,
            SelectedProgramId = programId,
            Mappings = await _db.ProgramCourses.AsNoTracking()
                .Include(pc => pc.Course)
                .OrderBy(pc => pc.Course!.CourseCode).ThenBy(pc => pc.Course!.CourseName)
                .Select(pc => new ProgramSubjectMappingItem
                {
                    ProgramCourseId = pc.ProgramCourseId,
                    ProgramId = pc.ProgramId,
                    CourseId = pc.CourseId,
                    CourseName = pc.Course!.CourseName,
                    CourseCode = pc.Course.CourseCode
                }).ToListAsync()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProgram(string programCode, string programName)
    {
        programCode = NormalizeCode(programCode);
        programName = NormalizeName(programName);
        var error = ValidateProgram(programCode, programName);
        if (error != null) return Fail(error);
        if (await _db.AcademicPrograms.AnyAsync(p => p.ProgramCode == programCode)) return Fail($"Program code {programCode} already exists.");
        if (await _db.AcademicPrograms.AnyAsync(p => p.ProgramName == programName)) return Fail("A program with the same name already exists.");

        var program = new AcademicProgram
        {
            ProgramId = await FormattedIdGenerator.NextAsync(_db.AcademicPrograms, p => p.ProgramId, "PRG"),
            ProgramCode = programCode,
            ProgramName = programName
        };
        _db.AcademicPrograms.Add(program);
        await _db.SaveChangesAsync();
        return Success($"Program {programCode} added.", program.ProgramId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgram(string programId, string programCode, string programName)
    {
        var p = await _db.AcademicPrograms.FindAsync(programId);
        if (p is null) return Fail("Program not found.");
        programCode = NormalizeCode(programCode);
        programName = NormalizeName(programName);
        var error = ValidateProgram(programCode, programName);
        if (error != null) return Fail(error, programId);
        if (await _db.AcademicPrograms.AnyAsync(x => x.ProgramId != programId && x.ProgramCode == programCode)) return Fail("Program code already exists.", programId);
        if (await _db.AcademicPrograms.AnyAsync(x => x.ProgramId != programId && x.ProgramName == programName)) return Fail("Program name already exists.", programId);

        p.ProgramCode = programCode;
        p.ProgramName = programName;
        await _db.SaveChangesAsync();
        return Success("Program updated.", programId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProgram(string programId)
    {
        var p = await _db.AcademicPrograms.FindAsync(programId);
        if (p is null) return Fail("Program not found.");
        if (await _db.ClassGroups.AnyAsync(c => c.ProgramId == programId) || await _db.StudentClasses.AnyAsync(s => s.ProgramId == programId))
            return Fail("This program is already used by classes or students, so it cannot be deleted.", programId);

        var mappings = await _db.ProgramCourses.Where(x => x.ProgramId == programId).ToListAsync();
        var courseIds = mappings.Select(x => x.CourseId).Distinct().ToList();
        _db.ProgramCourses.RemoveRange(mappings);
        _db.AcademicPrograms.Remove(p);
        await _db.SaveChangesAsync();
        await RemoveOrphanCourses(courseIds);
        return Success("Program deleted.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubject(string programId, string? courseCode, string courseName)
    {
        var program = await _db.AcademicPrograms.FindAsync(programId);
        if (program is null) return Fail("Please select a valid program.");
        courseCode = NormalizeOptionalCode(courseCode);
        courseName = NormalizeName(courseName);
        var error = ValidateSubject(courseCode, courseName);
        if (error != null) return Fail(error, programId);

        Course? byCode = courseCode == null ? null : await _db.Courses.FirstOrDefaultAsync(c => c.CourseCode == courseCode);
        Course? byName = await _db.Courses.FirstOrDefaultAsync(c => c.CourseName == courseName);
        if (byCode != null && byName != null && byCode.CourseId != byName.CourseId)
            return Fail("Subject code and subject name belong to two different existing subjects.", programId);

        Course? subject = byCode ?? byName;
        if (subject != null)
        {
            if (courseCode != null && subject.CourseCode != null && subject.CourseCode != courseCode)
                return Fail("This subject name already exists with another subject code.", programId);
            if (!string.Equals(subject.CourseName, courseName, StringComparison.OrdinalIgnoreCase))
                return Fail("This subject code already exists with another subject name.", programId);
        }
        else
        {
            subject = new Course
            {
                CourseId = await FormattedIdGenerator.NextAsync(_db.Courses, c => c.CourseId, "CRS"),
                CourseCode = courseCode,
                CourseName = courseName
            };
            _db.Courses.Add(subject);
        }

        if (await _db.ProgramCourses.AnyAsync(pc => pc.ProgramId == programId && pc.CourseId == subject.CourseId))
            return Fail($"{subject.CourseName} is already registered under {program.ProgramCode}.", programId);

        _db.ProgramCourses.Add(new ProgramCourse
        {
            ProgramCourseId = await FormattedIdGenerator.NextAsync(_db.ProgramCourses, x => x.ProgramCourseId, "PCR"),
            ProgramId = programId,
            CourseId = subject.CourseId
        });
        await _db.SaveChangesAsync();
        return Success($"{subject.CourseName} added to {program.ProgramCode}.", programId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgramSubject(string programId, string programCourseId, string courseId, string? courseCode, string courseName)
    {
        var mapping = await _db.ProgramCourses.FirstOrDefaultAsync(x => x.ProgramCourseId == programCourseId && x.ProgramId == programId);
        var subject = await _db.Courses.FindAsync(courseId);
        if (mapping is null || subject is null) return Fail("Program subject not found.", programId);

        courseCode = NormalizeOptionalCode(courseCode);
        courseName = NormalizeName(courseName);
        var error = ValidateSubject(courseCode, courseName);
        if (error != null) return Fail(error, programId);
        if (await _db.Courses.AnyAsync(c => c.CourseId != courseId && c.CourseName == courseName)) return Fail("Another subject already uses that name.", programId);
        if (courseCode != null && await _db.Courses.AnyAsync(c => c.CourseId != courseId && c.CourseCode == courseCode)) return Fail("Another subject already uses that code.", programId);

        subject.CourseCode = courseCode;
        subject.CourseName = courseName;
        await _db.SaveChangesAsync();
        return Success("Subject updated. Existing timetable rows will show the new subject name immediately.", programId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProgramSubject(string programId, string programCourseId)
    {
        var mapping = await _db.ProgramCourses.Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.ProgramCourseId == programCourseId && x.ProgramId == programId);
        if (mapping is null) return Fail("Program subject not found.", programId);

        var classIds = await _db.ClassGroups.Where(c => c.ProgramId == mapping.ProgramId).Select(c => c.ClassId).ToListAsync();
        if (await _db.Timetables.AnyAsync(t => classIds.Contains(t.ClassId) && t.CourseId == mapping.CourseId))
            return Fail("This subject is already used in this program's timetable. Remove those timetable lessons first.", programId);

        var courseId = mapping.CourseId;
        _db.ProgramCourses.Remove(mapping);
        await _db.SaveChangesAsync();
        await RemoveOrphanCourses(new[] { courseId });
        return Success("Subject removed from program.", programId);
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

    private static string NormalizeCode(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static string? NormalizeOptionalCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string NormalizeName(string? value) => Regex.Replace((value ?? "").Trim(), "\\s+", " ");

    private static string? ValidateProgram(string code, string name)
    {
        if (!ProgramCodePattern.IsMatch(code)) return "Program code must be 2-20 characters using only A-Z, 0-9 or hyphen.";
        if (name.Length < 3 || name.Length > 150) return "Program name must be between 3 and 150 characters.";
        return null;
    }

    private static string? ValidateSubject(string? code, string name)
    {
        if (code != null && !SubjectCodePattern.IsMatch(code)) return "Subject code must be 2-30 characters using only A-Z, 0-9 or hyphen.";
        if (name.Length < 2 || name.Length > 150) return "Subject name must be between 2 and 150 characters.";
        return null;
    }

    private IActionResult Success(string message, string? programId = null)
    {
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index), new { programId });
    }

    private IActionResult Fail(string message, string? programId = null)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index), new { programId });
    }
}
