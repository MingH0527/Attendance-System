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
        if (userId == null) return RedirectToAction("Login", "Account");
        await TryLinkLegacyProfileAsync(userId);
        if (!await db.TeacherCourses.AnyAsync(tc => tc.UserId == userId)) return RedirectToAction(nameof(Create));

        var entries = await db.Timetables.AsNoTracking().Where(t => t.TeacherId == userId)
            .Include(t => t.Course).Include(t => t.ClassGroup).Include(t => t.Classroom)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime).ToListAsync();
        return View(entries);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");
        var teacher = await db.Teacher.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId);
        var programs = await db.AcademicPrograms.AsNoTracking().OrderBy(p => p.ProgramCode).ToListAsync();
        string? preProgram = teacher == null ? null : programs.FirstOrDefault(p => p.ProgramCode.ToLower() == (teacher.Program ?? "").ToLower())?.ProgramId;
        return View(new TeacherProfileSetupViewModel { Programs = programs, ProgramId = preProgram });
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
    public async Task<IActionResult> Create(TeacherProfileSetupViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");
        if (string.IsNullOrWhiteSpace(model.ProgramId) || string.IsNullOrWhiteSpace(model.CourseId))
        {
            ModelState.AddModelError("", "Please select a program and subject.");
            model.Programs = await db.AcademicPrograms.AsNoTracking().OrderBy(p => p.ProgramCode).ToListAsync();
            return View(model);
        }

        var program = await db.AcademicPrograms.FindAsync(model.ProgramId);
        var course = await db.Courses.FindAsync(model.CourseId);
        bool mapped = await db.ProgramCourses.AnyAsync(pc => pc.ProgramId == model.ProgramId && pc.CourseId == model.CourseId);
        if (program == null || course == null || !mapped)
        {
            ModelState.AddModelError("", "The selected subject does not belong to this program.");
            model.Programs = await db.AcademicPrograms.AsNoTracking().OrderBy(p => p.ProgramCode).ToListAsync();
            return View(model);
        }

        var teacher = await db.Teacher.FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher == null)
        {
            teacher = new Teacher
            {
                TeacherId = await FormattedIdGenerator.NextAsync(db.Teacher, t => t.TeacherId, "TCH"),
                UserId = userId
            };
            db.Teacher.Add(teacher);
        }
        teacher.Program = program.ProgramCode;
        teacher.Subject = course.CourseName;

        if (!await db.TeacherCourses.AnyAsync(tc => tc.UserId == userId && tc.CourseId == course.CourseId))
        {
            db.TeacherCourses.Add(new TeacherCourse
            {
                TeacherCourseId = await FormattedIdGenerator.NextAsync(db.TeacherCourses, x => x.TeacherCourseId, "TCR"),
                UserId = userId,
                CourseId = course.CourseId,
                RegisteredDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
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
