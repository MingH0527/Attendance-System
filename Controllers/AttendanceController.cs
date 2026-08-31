using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Teacher")]
public class AttendanceController : Controller
{
    private readonly DB db;

    public AttendanceController(DB db)
    {
        this.db = db;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateCode(
        string timetableId
    )
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        // Make sure this timetable belongs
        // to the logged-in teacher
        var timetable = await db.Timetables
            .FirstOrDefaultAsync(t =>
                t.TimetableId == timetableId &&
                t.TeacherId == userId
            );

        if (timetable == null)
        {
            return NotFound();
        }

        DateTime now = DateTime.Now;

        // Must be correct day
        if (now.DayOfWeek != timetable.DayOfWeek)
        {
            TempData["AttendanceError"] =
                $"Attendance code can only be generated on {timetable.DayOfWeek}.";

            return RedirectToAction(
                "Timetable",
                "Teacher"
            );
        }

        // Must be during class time
        if (
            now.TimeOfDay < timetable.StartTime ||
            now.TimeOfDay > timetable.EndTime
        )
        {
            TempData["AttendanceError"] =
                $"Code can only be generated between " +
                $"{timetable.StartTime:hh\\:mm} and " +
                $"{timetable.EndTime:hh\\:mm}.";

            return RedirectToAction(
                "Timetable",
                "Teacher"
            );
        }
        

        // Generate 6-digit code
        string code;

        do
        {
            code = RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();
        }
        while (
            await db.AttendanceSessions.AnyAsync(
                s =>
                    s.Code == code &&
                    s.IsActive &&
                    s.ExpiresAt > now
            )
        );

        // Check whether teacher already
        // generated attendance today
        var existingSession =
            await db.AttendanceSessions
                .FirstOrDefaultAsync(s =>
                    s.TimetableId == timetableId &&
                    s.SessionDate == now.Date
                );

        // Code valid for 10 minutes
        DateTime classEnd =
             now.Date.Add(timetable.EndTime);

        DateTime expiresAt =
            now.AddMinutes(10);

        // Code cannot remain valid after class ends
        if (expiresAt > classEnd)
        {
            expiresAt = classEnd;
        }

        if (existingSession == null)
        {
            var session =
                new AttendanceSession
                {
                    AttendanceSessionId =
                        await FormattedIdGenerator
                            .NextAsync(
                                db.AttendanceSessions,
                                x => x.AttendanceSessionId,
                                "ATS"
                            ),

                    TimetableId =
                        timetable.TimetableId,

                    SessionDate =
                        now.Date,

                    Code =
                        code,

                    GeneratedAt =
                        now,

                    ExpiresAt =
                        expiresAt,

                    IsActive =
                        true
                };

            db.AttendanceSessions.Add(session);
        }
        else
        {
            // Regenerate code instead of
            // creating duplicated session
            existingSession.Code = code;
            existingSession.GeneratedAt = now;
            existingSession.ExpiresAt = expiresAt;
            existingSession.IsActive = true;
        }

        await db.SaveChangesAsync();

        return RedirectToAction(
            "Timetable",
            "Teacher"
        );
    }
}