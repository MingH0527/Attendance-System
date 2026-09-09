using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize]
public class ProgramController : Controller
{
    private readonly DB db;

    public ProgramController(DB db)
    {
        this.db = db;
    }

    // GET: /Program/RegisterProgram
    [HttpGet]
    public async Task<IActionResult> RegisterProgram()
    {
        string? userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = new ProgramRegisterViewModel
        {
            Position = user.Position,
            AvailablePrograms = await db.AcademicPrograms.AsNoTracking().ToListAsync(),
            AvailableClasses = await db.ClassGroups.AsNoTracking().ToListAsync(),
            AvailableCourses = await db.Courses.AsNoTracking().ToListAsync(),
        };

        if (user.Position == "Student")
        {
            StudentClass? existing = await db.StudentClasses
                .Include(sc => sc.Program)
                .Include(sc => sc.ClassGroup)
                .FirstOrDefaultAsync(sc => sc.UserId == userId);

            if (existing != null)
            {
                model.SelectedProgramId = existing.ProgramId;
                model.SelectedClassId = existing.ClassId;
                model.CurrentStudentId = existing.StudentId;
                model.CurrentProgramName = existing.Program?.ProgramName;
                model.CurrentClassName = existing.ClassGroup?.ClassName;
            }
        }
        else if (user.Position == "Teacher")
        {
            List<TeacherCourse> existing = await db.TeacherCourses
                .Include(tc => tc.Course)
                .Where(tc => tc.UserId == userId)
                .ToListAsync();

            model.SelectedCourseIds = existing.Select(tc => tc.CourseId).ToList();
            model.CurrentCourseNames = existing.Select(tc => tc.Course!.CourseName).ToList();
        }

        return View(model);
    }

    // POST: /Program/RegisterProgram
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterProgram(ProgramRegisterViewModel model)
    {
        string? userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        User? user = await db.Users.FindAsync(userId);

        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (user.Position == "Student")
        {
            ModelState.Remove(nameof(model.SelectedCourseIds));

            if (string.IsNullOrWhiteSpace(model.SelectedProgramId))
            {
                ModelState.AddModelError(
                    nameof(model.SelectedProgramId),
                    "Please selecet at least one course"
                );
            }

            if (string.IsNullOrWhiteSpace(model.SelectedClassId))
            {
                ModelState.AddModelError(
                    nameof(model.SelectedClassId),
                    "Please select class"
                );
            }
            else if (!string.IsNullOrWhiteSpace(model.SelectedProgramId))
            {
                // The browser filters the list, but a hand-crafted post could
                // still pair a class with the wrong program - reject that here.
                bool belongsToProgram = await db.ClassGroups
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.ClassId == model.SelectedClassId &&
                        c.ProgramId == model.SelectedProgramId);

                if (!belongsToProgram)
                {
                    ModelState.AddModelError(
                        nameof(model.SelectedClassId),
                        "That class does not belong to the selected program."
                    );
                }
            }
        }
        else if (user.Position == "Teacher")
        {
            ModelState.Remove(nameof(model.SelectedProgramId));
            ModelState.Remove(nameof(model.SelectedClassId));

            if (model.SelectedCourseIds == null ||
                !model.SelectedCourseIds.Any())
            {
                ModelState.AddModelError(
                    nameof(model.SelectedCourseIds),
                    "Please select at least one course."
                );
            }
        }

        if (!ModelState.IsValid)
        {
            model.Position = user.Position;
            model.AvailablePrograms = await db.AcademicPrograms.AsNoTracking().ToListAsync();
            model.AvailableClasses = await db.ClassGroups.AsNoTracking().ToListAsync();
            model.AvailableCourses = await db.Courses.AsNoTracking().ToListAsync();
            return View(model);
        }

        if (user.Position == "Student")
        {
            StudentClass? existing = await db.StudentClasses
                .FirstOrDefaultAsync(sc => sc.UserId == userId);

            if (existing != null)
            {

                existing.ProgramId = model.SelectedProgramId;
                existing.ClassId = model.SelectedClassId!;
                existing.RegisteredDate = DateTime.Now;
            }
            else
            {

                string newStudentId = await GenerateStudentIdAsync();

                db.StudentClasses.Add(new StudentClass
                {
                    StudentClassId = await FormattedIdGenerator.NextAsync(
                        db.StudentClasses,
                        sc => sc.StudentClassId,
                        "STC"
                    ),
                    UserId = userId,
                    StudentId = newStudentId,
                    ProgramId = model.SelectedProgramId,
                    ClassId = model.SelectedClassId!
                });
            }
        }
        else if (user.Position == "Teacher")
        {
            var oldRecords = db.TeacherCourses.Where(tc => tc.UserId == userId);
            db.TeacherCourses.RemoveRange(oldRecords);

            foreach (string courseId in model.SelectedCourseIds ?? new List<string>())
            {
                db.TeacherCourses.Add(new TeacherCourse
                {
                    TeacherCourseId = await FormattedIdGenerator.NextAsync(
                        db.TeacherCourses,
                        tc => tc.TeacherCourseId,
                        "TCR"
                    ),
                    UserId = userId,
                    CourseId = courseId
                });
            }
        }

        await db.SaveChangesAsync();

        TempData["Message"] = "Registration saved successfully.";
        return RedirectToAction("Index", "MyProfile");
    }


    private async Task<string> GenerateStudentIdAsync()
    {
        int year = DateTime.Now.Year;
        string prefix = $"S{year}";

        int countThisYear = await db.StudentClasses
            .CountAsync(sc => sc.StudentId.StartsWith(prefix));

        return $"{prefix}{(countThisYear + 1):D3}";
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}