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

    public AttendanceController(
        DB db)
    {
        this.db =
            db;
    }


    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }


    // ==================================================
    // GENERATE ATTENDANCE CODE
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateCode(
        string timetableId,
        string? replacementId)
    {
        string? userId =
            GetCurrentUserId();


        if (
            userId ==
            null
        )
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }


        DateTime now =
            DateTime.Now;


        Timetable? timetable;

        TimeSpan actualStartTime;

        TimeSpan actualEndTime;


        // ==================================================
        // REPLACEMENT CLASS
        // ==================================================

        if (
            !string.IsNullOrWhiteSpace(
                replacementId
            )
        )
        {
            ReplacementSession? replacement =
                await db.ReplacementSessions
                    .Include(r =>
                        r.SourceTimetable
                    )
                    .ThenInclude(t =>
                        t!.Term
                    )
                    .FirstOrDefaultAsync(r =>
                        r.ReplacementId ==
                            replacementId &&
                        r.TeacherId ==
                            userId
                    );


            if (
                replacement ==
                null ||
                replacement.SourceTimetable ==
                null
            )
            {
                return NotFound();
            }


            if (
                !string.Equals(
                    replacement.Status,
                    "Scheduled",
                    StringComparison
                        .OrdinalIgnoreCase
                )
            )
            {
                TempData["AttendanceError"] =
                    "This replacement class is not available for attendance.";

                return RedirectToAction(
                    "AttendanceCode",
                    "Teacher"
                );
            }


            if (
                replacement
                    .ReplacementDate
                    .Date !=
                now.Date
            )
            {
                TempData["AttendanceError"] =
                    "Attendance code can only be generated on the replacement class date.";

                return RedirectToAction(
                    "AttendanceCode",
                    "Teacher"
                );
            }


            timetable =
                replacement
                    .SourceTimetable;


            if (
                timetable.Term ==
                    null ||
                timetable.Term.Status !=
                    "Active"
            )
            {
                TempData["AttendanceError"] =
                    "This class is not in the active semester.";

                return RedirectToAction(
                    "AttendanceCode",
                    "Teacher"
                );
            }


            actualStartTime =
                replacement.StartTime;

            actualEndTime =
                replacement.EndTime;
        }

        // ==================================================
        // NORMAL CLASS
        // ==================================================

        else
        {
            timetable =
                await db.Timetables
                    .Include(t =>
                        t.Term
                    )
                    .FirstOrDefaultAsync(t =>
                        t.TimetableId ==
                            timetableId &&
                        t.TeacherId ==
                            userId &&
                        t.Term != null &&
                        t.Term.Status ==
                            "Active"
                    );


            if (
                timetable ==
                null
            )
            {
                return NotFound();
            }


            if (
                now.DayOfWeek !=
                timetable.DayOfWeek
            )
            {
                TempData["AttendanceError"] =
                    $"Attendance code can only be generated on {timetable.DayOfWeek}.";

                return RedirectToAction(
                    "AttendanceCode",
                    "Teacher"
                );
            }


            TimetableException? exception =
                await db.TimetableExceptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e =>
                        e.TimetableId ==
                            timetable.TimetableId &&
                        e.OccurrenceDate.Date ==
                            now.Date &&
                        e.Decision !=
                            "Pending"
                    );


            if (
                exception !=
                    null &&
                IsCancelledValue(
                    exception.Decision
                )
            )
            {
                TempData["AttendanceError"] =
                    "This class has been cancelled.";

                return RedirectToAction(
                    "AttendanceCode",
                    "Teacher"
                );
            }


            actualStartTime =
                exception
                    ?.OverrideStartTime
                ?? timetable.StartTime;


            actualEndTime =
                exception
                    ?.OverrideEndTime
                ?? timetable.EndTime;
        }


        // ==================================================
        // CLASS TIME VALIDATION
        // ==================================================

        if (
            now.TimeOfDay <
                actualStartTime ||
            now.TimeOfDay >
                actualEndTime
        )
        {
            TempData["AttendanceError"] =
                $"Code can only be generated between " +
                $"{actualStartTime:hh\\:mm} and " +
                $"{actualEndTime:hh\\:mm}.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }


        // ==================================================
        // GENERATE UNIQUE 6-DIGIT CODE
        // ==================================================

        string code;


        do
        {
            code =
                RandomNumberGenerator
                    .GetInt32(
                        100000,
                        1000000
                    )
                    .ToString();
        }
        while (
            await db.AttendanceSessions
                .AnyAsync(s =>
                    s.Code ==
                        code &&
                    s.IsActive &&
                    s.ExpiresAt >
                        now
                )
        );


        // ==================================================
        // EXISTING SESSION
        // ==================================================

        AttendanceSession? existingSession =
            await db.AttendanceSessions
                .FirstOrDefaultAsync(s =>
                    s.TimetableId ==
                        timetable.TimetableId &&
                    s.SessionDate ==
                        now.Date
                );


        // ==================================================
        // EXPIRY
        // ==================================================

        DateTime classEnd =
            now.Date.Add(
                actualEndTime
            );


        DateTime expiresAt =
            now.AddMinutes(
                10
            );


        if (
            expiresAt >
            classEnd
        )
        {
            expiresAt =
                classEnd;
        }


        // ==================================================
        // CREATE / REGENERATE
        // ==================================================

        if (
            existingSession ==
            null
        )
        {
            AttendanceSession session =
                new()
                {
                    AttendanceSessionId =
                        await FormattedIdGenerator
                            .NextAsync(
                                db.AttendanceSessions,
                                x =>
                                    x.AttendanceSessionId,
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


            db.AttendanceSessions.Add(
                session
            );
        }
        else
        {
            existingSession.Code =
                code;

            existingSession.GeneratedAt =
                now;

            existingSession.ExpiresAt =
                expiresAt;

            existingSession.IsActive =
                true;
        }


        await db.SaveChangesAsync();


        TempData["AttendanceSuccess"] =
            "Attendance code generated successfully.";


        return RedirectToAction(
            "AttendanceCode",
            "Teacher"
        );
    }


    private static bool IsCancelledValue(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return false;
        }


        return value
            .Trim()
            .ToLower()
            .Contains(
                "cancel"
            );
    }
}