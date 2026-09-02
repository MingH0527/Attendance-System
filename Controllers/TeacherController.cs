using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    private readonly DB db;
    public TeacherController(DB db) => this.db = db;

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return RedirectToAction("Login", "Account");

        await TryLinkLegacyProfileAsync(userId);
        if (!await db.TeacherCourses.AnyAsync(tc => tc.UserId == userId)) return RedirectToAction(nameof(Create));

        ViewBag.PasskeyCount = await db.UserPasskeys.CountAsync(p => p.UserId == user.UserId);
        ViewBag.TimetableCount = await db.Timetables.CountAsync(t => t.TeacherId == user.UserId);
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Timetable()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
            return RedirectToAction("Login", "Account");

        await TryLinkLegacyProfileAsync(userId);

        if (!await db.TeacherCourses.AnyAsync(
                tc => tc.UserId == userId))
        {
            return RedirectToAction(nameof(Create));
        }

        var entries = await db.Timetables
            .AsNoTracking()
            .Where(t => t.TeacherId == userId)
            .Include(t => t.Course)
            .Include(t => t.ClassGroup)
            .Include(t => t.Classroom)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .ToListAsync();

        // Get all timetable IDs shown on this page
        var timetableIds = entries
            .Select(t => t.TimetableId)
            .ToList();

        // Get today's attendance session/code
        var attendanceSessions = await db.AttendanceSessions
            .AsNoTracking()
            .Where(a =>
                timetableIds.Contains(a.TimetableId) &&
                a.SessionDate == DateTime.Today &&
                a.IsActive)
            .ToDictionaryAsync(
                a => a.TimetableId,
                a => a
            );

        // Pass attendance codes to the View
        ViewBag.AttendanceSessions = attendanceSessions;

        return View(entries);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
            return RedirectToAction("Login", "Account");

        await TryLinkLegacyProfileAsync(userId);

        // 已经选择过的 Program
        var selectedProgramIds = await db.TeacherPrograms
            .AsNoTracking()
            .Where(tp => tp.UserId == userId)
            .Select(tp => tp.ProgramId)
            .ToListAsync();

        // 已经选择过的 Subject
        var selectedCourseIds = await db.TeacherCourses
            .AsNoTracking()
            .Where(tc => tc.UserId == userId)
            .Select(tc => tc.CourseId)
            .ToListAsync();

        var model = new TeacherProfileSetupViewModel
        {
            SelectedProgramIds = selectedProgramIds,
            SelectedCourseIds = selectedCourseIds
        };

        await LoadTeacherSetupOptionsAsync(model);

        return View(model);
    }
    private async Task LoadTeacherSetupOptionsAsync(
    TeacherProfileSetupViewModel model)
    {
        model.Programs = await db.AcademicPrograms
            .AsNoTracking()
            .OrderBy(p => p.ProgramCode)
            .ToListAsync();

        model.Subjects = await db.ProgramCourses
            .AsNoTracking()
            .Include(pc => pc.Course)
            .Where(pc => pc.Course != null)
            .OrderBy(pc => pc.ProgramId)
            .ThenBy(pc => pc.Course!.CourseName)
            .Select(pc => new TeacherSubjectOptionViewModel
            {
                ProgramId = pc.ProgramId,
                CourseId = pc.CourseId,
                CourseName = pc.Course!.CourseName,
                CourseCode = pc.Course.CourseCode
            })
            .ToListAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Subjects(string programId)
    {
        var rows = await db.ProgramCourses.AsNoTracking().Where(pc => pc.ProgramId == programId).Include(pc => pc.Course)
            .OrderBy(pc => pc.Course!.CourseName)
            .Select(pc => new { id = pc.CourseId, name = pc.Course!.CourseName, code = pc.Course.CourseCode }).ToListAsync();
        return Json(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
    TeacherProfileSetupViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
            return RedirectToAction("Login", "Account");

        // Remove duplicate values
        model.SelectedProgramIds = model.SelectedProgramIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        model.SelectedCourseIds = model.SelectedCourseIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        // Must select program
        if (model.SelectedProgramIds.Count == 0)
        {
            ModelState.AddModelError(
                "",
                "Please select at least one program.");
        }

        // Must select subject
        if (model.SelectedCourseIds.Count == 0)
        {
            ModelState.AddModelError(
                "",
                "Please select at least one subject.");
        }

        if (!ModelState.IsValid)
        {
            await LoadTeacherSetupOptionsAsync(model);
            return View(model);
        }

        // Make sure programs really exist
        var validProgramIds = await db.AcademicPrograms
            .AsNoTracking()
            .Where(p => model.SelectedProgramIds.Contains(p.ProgramId))
            .Select(p => p.ProgramId)
            .ToListAsync();

        if (validProgramIds.Count != model.SelectedProgramIds.Count)
        {
            ModelState.AddModelError(
                "",
                "One or more selected programs are invalid.");

            await LoadTeacherSetupOptionsAsync(model);

            return View(model);
        }

        // Check whether selected subjects belong to selected programs
        var validMappings = await db.ProgramCourses
            .AsNoTracking()
            .Where(pc =>
                model.SelectedProgramIds.Contains(pc.ProgramId) &&
                model.SelectedCourseIds.Contains(pc.CourseId))
            .Select(pc => new
            {
                pc.ProgramId,
                pc.CourseId
            })
            .ToListAsync();

        var validCourseIds = validMappings
            .Select(x => x.CourseId)
            .Distinct()
            .ToList();

        // Subject selected but not under selected programs
        if (validCourseIds.Count != model.SelectedCourseIds.Count)
        {
            ModelState.AddModelError(
                "",
                "One or more selected subjects do not belong to the selected programs.");

            await LoadTeacherSetupOptionsAsync(model);

            return View(model);
        }

        // Every selected program should have at least one subject
        foreach (var programId in model.SelectedProgramIds)
        {
            bool hasSubject =
                validMappings.Any(x => x.ProgramId == programId);

            if (!hasSubject)
            {
                ModelState.AddModelError(
                    "",
                    "Please select at least one subject for every selected program.");

                await LoadTeacherSetupOptionsAsync(model);

                return View(model);
            }
        }

        // Create / update Teacher
        var teacher = await db.Teacher
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (teacher == null)
        {
            teacher = new Teacher
            {
                TeacherId =
                    await FormattedIdGenerator.NextAsync(
                        db.Teacher,
                        t => t.TeacherId,
                        "TCH"),

                UserId = userId
            };

            db.Teacher.Add(teacher);
        }

        // Keep legacy Program / Subject fields
        // so older pages do not suddenly break
        var programCodes = await db.AcademicPrograms
            .AsNoTracking()
            .Where(p =>
                model.SelectedProgramIds.Contains(p.ProgramId))
            .OrderBy(p => p.ProgramCode)
            .Select(p => p.ProgramCode)
            .ToListAsync();

        var courseNames = await db.Courses
            .AsNoTracking()
            .Where(c =>
                model.SelectedCourseIds.Contains(c.CourseId))
            .OrderBy(c => c.CourseName)
            .Select(c => c.CourseName)
            .ToListAsync();

        teacher.Program = string.Join(", ", programCodes);
        teacher.Subject = string.Join(", ", courseNames);

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
           //update teacher programs

            var existingPrograms = await db.TeacherPrograms
                .Where(tp => tp.UserId == userId)
                .ToListAsync();

            var programsToRemove = existingPrograms
                .Where(tp =>
                    !model.SelectedProgramIds.Contains(tp.ProgramId))
                .ToList();

            db.TeacherPrograms.RemoveRange(programsToRemove);

            var existingProgramIds = existingPrograms
                .Select(tp => tp.ProgramId)
                .ToHashSet();

            foreach (var programId in model.SelectedProgramIds)
            {
                if (!existingProgramIds.Contains(programId))
                {
                    db.TeacherPrograms.Add(
                        new TeacherProgram
                        {
                            UserId = userId,
                            ProgramId = programId,
                            RegisteredDate = DateTime.Now
                        });
                }
            }
            //update teacher subjects
       
            var existingCourses = await db.TeacherCourses
                .Where(tc => tc.UserId == userId)
                .ToListAsync();

            var coursesToRemove = existingCourses
                .Where(tc =>
                    !model.SelectedCourseIds.Contains(tc.CourseId))
                .ToList();

            db.TeacherCourses.RemoveRange(coursesToRemove);

            await db.SaveChangesAsync();

            var remainingCourseIds = await db.TeacherCourses
                .Where(tc => tc.UserId == userId)
                .Select(tc => tc.CourseId)
                .ToListAsync();

            // Add new courses
            foreach (var courseId in model.SelectedCourseIds)
            {
                if (!remainingCourseIds.Contains(courseId))
                {
                    var teacherCourse =
                        new TeacherCourse
                        {
                            TeacherCourseId =
                                await FormattedIdGenerator.NextAsync(
                                    db.TeacherCourses,
                                    x => x.TeacherCourseId,
                                    "TCR"),

                            UserId = userId,
                            CourseId = courseId,
                            RegisteredDate = DateTime.Now
                        };

                    db.TeacherCourses.Add(teacherCourse);

                    // Save here so next generated TCR ID
                    // will not duplicate
                    await db.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToAction("Index", "MyProfile");
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = GetCurrentUserId();
        var teacher = await db.Teacher.FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher == null) return RedirectToAction(nameof(Create));
        await TryLinkLegacyProfileAsync(userId!);
        if (!await db.TeacherCourses.AnyAsync(tc => tc.UserId == userId)) return RedirectToAction(nameof(Create));
        return View(teacher);
    }

    private async Task TryLinkLegacyProfileAsync(string userId)
    {
        if (await db.TeacherCourses.AnyAsync(tc => tc.UserId == userId)) return;
        var teacher = await db.Teacher.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher == null || string.IsNullOrWhiteSpace(teacher.Program) || string.IsNullOrWhiteSpace(teacher.Subject)) return;
        var program = await db.AcademicPrograms.AsNoTracking().FirstOrDefaultAsync(p => p.ProgramCode.ToLower() == teacher.Program.ToLower());
        if (program == null) return;
        var mapping = await db.ProgramCourses.AsNoTracking().Include(pc => pc.Course)
            .FirstOrDefaultAsync(pc => pc.ProgramId == program.ProgramId && pc.Course != null && pc.Course.CourseName.ToLower() == teacher.Subject.ToLower());
        if (mapping == null) return;
        db.TeacherCourses.Add(new TeacherCourse
        {
            TeacherCourseId = await FormattedIdGenerator.NextAsync(db.TeacherCourses, x => x.TeacherCourseId, "TCR"),
            UserId = userId,
            CourseId = mapping.CourseId,
            RegisteredDate = DateTime.Now
        });
        await db.SaveChangesAsync();
    }
}
