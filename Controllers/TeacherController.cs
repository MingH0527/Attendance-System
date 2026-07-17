using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    private readonly DB db;

    public TeacherController(DB db)
    {
        this.db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        User? user =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.UserId == userId.Value
                );

        if (user == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        int passkeyCount =
            await db.UserPasskeys.CountAsync(
                p => p.UserId == user.UserId
            );

        int timetableCount =
            await db.Timetables.CountAsync(
                t => t.TeacherId == user.UserId
            );

        ViewBag.PasskeyCount =
            passkeyCount;

        ViewBag.TimetableCount =
            timetableCount;

        return View(user);
    }

    private int? GetCurrentUserId()
    {
        string? userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        return int.TryParse(
            userIdValue,
            out int userId
        )
            ? userId
            : null;
    }
    [HttpGet]
    public async Task<IActionResult> Timetable()
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        List<Timetable> entries =
            await db.Timetables
                .AsNoTracking()
                .Where(
                    t => t.TeacherId == userId.Value
                )
                .Include(t => t.Course)
                .Include(t => t.ClassGroup)
                .Include(t => t.Classroom)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartTime)
                .ToListAsync();

        return View(entries);
    }
}