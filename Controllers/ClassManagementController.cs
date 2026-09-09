using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

/// <summary>
/// Class Management.
///
/// A "class" here is a student group inside a program (DCS1, DCS2), not a
/// physical room. Timetables are built per class, so a program with no
/// class cannot be scheduled at all.
///
/// Create and Update only - classes are referenced by timetables, student
/// registrations and attendance, so deletion is deliberately not offered.
/// </summary>
[Authorize(Roles = "Admin")]
public class ClassManagementController : Controller
{
    private const int MaxClassesPerProgram =
        ClassManagementViewModel.MaxClassesPerProgram;

    private readonly DB _db;

    public ClassManagementController(DB db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string sort = "asc",
        string? search = null)
    {
        bool descending =
            string.Equals(sort, "desc", StringComparison.OrdinalIgnoreCase);

        IQueryable<AcademicProgram> query =
            _db.AcademicPrograms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();

            query = query.Where(p =>
                p.ProgramId.Contains(term) ||
                p.ProgramName.Contains(term));
        }

        query = descending
            ? query.OrderByDescending(p => p.ProgramId)
            : query.OrderBy(p => p.ProgramId);

        List<AcademicProgram> programs = await query.ToListAsync();

        List<ProgramClassRow> rows = [];

        foreach (AcademicProgram program in programs)
        {
            List<ClassRow> classes = await _db.ClassGroups
                .AsNoTracking()
                .Where(c => c.ProgramId == program.ProgramId)
                .OrderBy(c => c.ClassName)
                .Select(c => new ClassRow
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    TimetableCount = c.Timetables.Count(),
                    StudentCount = _db.StudentClasses
                        .Count(sc => sc.ClassId == c.ClassId)
                })
                .ToListAsync();

            rows.Add(new ProgramClassRow
            {
                ProgramId = program.ProgramId,
                ProgramName = program.ProgramName,
                Classes = classes
            });
        }

        ClassManagementViewModel model = new()
        {
            Programs = rows,
            Sort = descending ? "desc" : "asc",
            Search = search,
            TotalPrograms = rows.Count,
            TotalClasses = rows.Sum(r => r.Classes.Count),
            ProgramsWithoutClass = rows.Count(r => r.HasNoClass)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string programId,
        string className,
        string sort = "asc")
    {
        className = Normalise(className);

        if (className.Length < 2 || className.Length > 150)
            return Fail("Class name must be between 2 and 150 characters.", sort);

        AcademicProgram? program = await _db.AcademicPrograms
            .FirstOrDefaultAsync(p => p.ProgramId == programId);

        if (program is null)
            return Fail("Program not found.", sort);

        int existing = await _db.ClassGroups
            .CountAsync(c => c.ProgramId == programId);

        if (existing >= MaxClassesPerProgram)
            return Fail(
                $"{program.ProgramName} already has the maximum of " +
                $"{MaxClassesPerProgram} classes.", sort);

        // ClassName is unique across the whole table, not just this program.
        if (await _db.ClassGroups.AnyAsync(c => c.ClassName == className))
            return Fail(
                $"A class named {className} already exists.", sort);

        string classId = await FormattedIdGenerator.NextAsync(
            _db.ClassGroups, c => c.ClassId, "CLS");

        _db.ClassGroups.Add(new ClassGroup
        {
            ClassId = classId,
            ClassName = className,
            ProgramId = programId
        });

        await _db.SaveChangesAsync();

        return Ok_(
            $"Class {className} added to {program.ProgramName}.", sort);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        string classId,
        string className,
        string sort = "asc")
    {
        className = Normalise(className);

        if (className.Length < 2 || className.Length > 150)
            return Fail("Class name must be between 2 and 150 characters.", sort);

        ClassGroup? group = await _db.ClassGroups
            .FirstOrDefaultAsync(c => c.ClassId == classId);

        if (group is null)
            return Fail("Class not found.", sort);

        if (await _db.ClassGroups.AnyAsync(c =>
                c.ClassName == className && c.ClassId != classId))
            return Fail(
                $"A class named {className} already exists.", sort);

        string previous = group.ClassName;

        if (previous == className)
            return Ok_("No change made.", sort);

        group.ClassName = className;

        await _db.SaveChangesAsync();

        return Ok_($"Class {previous} renamed to {className}.", sort);
    }

    // ---------- helpers ----------

    private static string Normalise(string? value) =>
        string.Join(' ',
            (value ?? "").Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private IActionResult Ok_(string message, string sort)
    {
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index), new { sort });
    }

    private IActionResult Fail(string message, string sort)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index), new { sort });
    }
}