using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

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
        int? userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

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
                .FirstOrDefaultAsync(sc => sc.UserId == userId.Value);

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
                .Where(tc => tc.UserId == userId.Value)
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
        int? userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        User? user = await db.Users.FindAsync(userId.Value);

        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (user.Position == "Student")
        {
            if (model.SelectedProgramId <= 0)
                ModelState.AddModelError(nameof(model.SelectedProgramId), "请选择科系/学位");

            if (model.SelectedClassId == null)
                ModelState.AddModelError(nameof(model.SelectedClassId), "请选择班级");
        }

        if (user.Position == "Teacher" && !model.SelectedCourseIds.Any())
        {
            ModelState.AddModelError(nameof(model.SelectedCourseIds), "请至少选择一门课程");
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
                .FirstOrDefaultAsync(sc => sc.UserId == userId.Value);

            if (existing != null)
            {
               
                existing.ProgramId = model.SelectedProgramId;
                existing.ClassId = model.SelectedClassId!.Value;
                existing.RegisteredDate = DateTime.Now;
            }
            else
            {
                
                string newStudentId = await GenerateStudentIdAsync();

                db.StudentClasses.Add(new StudentClass
                {
                    UserId = userId.Value,
                    StudentId = newStudentId,
                    ProgramId = model.SelectedProgramId,
                    ClassId = model.SelectedClassId!.Value
                });
            }
        }
        else if (user.Position == "Teacher")
        {
            var oldRecords = db.TeacherCourses.Where(tc => tc.UserId == userId.Value);
            db.TeacherCourses.RemoveRange(oldRecords);

            foreach (int courseId in model.SelectedCourseIds)
            {
                db.TeacherCourses.Add(new TeacherCourse
                {
                    UserId = userId.Value,
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

    private int? GetCurrentUserId()
    {
        string? userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(userIdValue, out int userId) ? userId : null;
    }
}