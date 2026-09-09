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

    // ==================================================
    // REGISTER PROGRAM - GET
    // ==================================================
    [HttpGet]
    public async Task<IActionResult> RegisterProgram()
    {
        string? userId =
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
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId
                );


        if (user == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        ProgramRegisterViewModel model =
            new()
            {
                Position =
                    user.Position,

                AvailablePrograms =
                    await db.AcademicPrograms
                        .AsNoTracking()
                        .ToListAsync(),

                AvailableClasses =
                    await db.ClassGroups
                        .AsNoTracking()
                        .ToListAsync(),

                AvailableCourses =
                    await db.Courses
                        .AsNoTracking()
                        .ToListAsync(),
            };


        if (user.Position == "Student")
        {
            StudentClass? existing =
                await db.StudentClasses
                    .AsNoTracking()
                    .Include(sc =>
                        sc.Program
                    )
                    .Include(sc =>
                        sc.ClassGroup
                    )
                    .FirstOrDefaultAsync(sc =>
                        sc.UserId ==
                        userId
                    );


            if (existing != null)
            {
                model.SelectedProgramId =
                    existing.ProgramId;


                model.SelectedClassId =
                    existing.ClassId;


                model.CurrentStudentId =
                    existing.StudentId;


                model.CurrentProgramName =
                    existing.Program
                        ?.ProgramName;


                model.CurrentClassName =
                    existing.ClassGroup
                        ?.ClassName;
            }
        }

        else if (
            user.Position ==
            "Teacher"
        )
        {
            List<TeacherCourse> existing =
                await db.TeacherCourses
                    .AsNoTracking()
                    .Include(tc =>
                        tc.Course
                    )
                    .Where(tc =>
                        tc.UserId ==
                        userId
                    )
                    .ToListAsync();


            model.SelectedCourseIds =
                existing
                    .Select(tc =>
                        tc.CourseId
                    )
                    .ToList();


            model.CurrentCourseNames =
                existing
                    .Where(tc =>
                        tc.Course != null
                    )
                    .Select(tc =>
                        tc.Course!
                            .CourseName
                    )
                    .ToList();
        }


        return View(
            model
        );
    }

    // ==================================================
    // REGISTER PROGRAM - POST
    // ==================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterProgram(
        ProgramRegisterViewModel model)
    {
        string? userId =
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
                .FindAsync(
                    userId
                );


        if (user == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        // ==================================================
        // student validation
        // ==================================================

        if (
            user.Position ==
            "Student"
        )
        {
            ModelState.Remove(
                nameof(
                    model.SelectedCourseIds
                )
            );


            if (
                string.IsNullOrWhiteSpace(
                    model.SelectedProgramId
                )
            )
            {
                ModelState.AddModelError(
                    nameof(
                        model.SelectedProgramId
                    ),
                    "Please select a program."
                );
            }


            if (
                string.IsNullOrWhiteSpace(
                    model.SelectedClassId
                )
            )
            {
                ModelState.AddModelError(
                    nameof(
                        model.SelectedClassId
                    ),
                    "Please select a class."
                );
            }
            else if (
                !string.IsNullOrWhiteSpace(
                    model.SelectedProgramId
                )
            )
            {

                bool belongsToProgram =
                    await db.ClassGroups
                        .AsNoTracking()
                        .AnyAsync(c =>
                            c.ClassId ==
                                model.SelectedClassId &&
                            c.ProgramId ==
                                model.SelectedProgramId
                        );


                if (
                    !belongsToProgram
                )
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedClassId
                        ),
                        "That class does not belong to the selected program."
                    );
                }
            }
        }

        // ==================================================
        // teacher validation
        // ==================================================

        else if (
            user.Position ==
            "Teacher"
        )
        {
            ModelState.Remove(
                nameof(
                    model.SelectedProgramId
                )
            );


            ModelState.Remove(
                nameof(
                    model.SelectedClassId
                )
            );


            if (
                model.SelectedCourseIds ==
                    null ||
                !model.SelectedCourseIds
                    .Any()
            )
            {
                ModelState.AddModelError(
                    nameof(
                        model.SelectedCourseIds
                    ),
                    "Please select at least one course."
                );
            }
        }


        // ==================================================
        // invalid model
        // ==================================================

        if (
            !ModelState.IsValid
        )
        {
            model.Position =
                user.Position;


            model.AvailablePrograms =
                await db.AcademicPrograms
                    .AsNoTracking()
                    .ToListAsync();


            model.AvailableClasses =
                await db.ClassGroups
                    .AsNoTracking()
                    .ToListAsync();


            model.AvailableCourses =
                await db.Courses
                    .AsNoTracking()
                    .ToListAsync();


            return View(
                model
            );
        }

        // ==================================================
        // STUDENT
        // ==================================================

        if (
            user.Position ==
            "Student"
        )
        {
            StudentClass? existing =
                await db.StudentClasses
                    .FirstOrDefaultAsync(sc =>
                        sc.UserId ==
                        userId
                    );


            DateTime registeredAt =
                DateTime.Now;


            // ==================================================
            // EXISTING STUDENT PROFILE
            // ==================================================

            if (
                existing !=
                null
            )
            {
                bool registrationChanged =
                    existing.ProgramId !=
                        model.SelectedProgramId ||
                    existing.ClassId !=
                        model.SelectedClassId;


                existing.ProgramId =
                    model.SelectedProgramId!;


                existing.ClassId =
                    model.SelectedClassId!;


                // Only reset registration date
                // if Student actually changed
                // Program or Class.

                if (
                    registrationChanged
                )
                {
                    existing.RegisteredDate =
                        registeredAt;
                }
                else
                {
                    // Same Program + Class.
                    // Keep original registration date.

                    registeredAt =
                        existing.RegisteredDate;
                }
            }

            // ==================================================
            // NEW STUDENT PROFILE
            // ==================================================

            else
            {
                string newStudentId =
                    await GenerateStudentIdAsync();


                StudentClass studentClass =
                    new()
                    {
                        StudentClassId =
                            await FormattedIdGenerator
                                .NextAsync(
                                    db.StudentClasses,
                                    sc =>
                                        sc.StudentClassId,
                                    "STC"
                                ),

                        UserId =
                            userId,

                        StudentId =
                            newStudentId,

                        ProgramId =
                            model.SelectedProgramId!,

                        ClassId =
                            model.SelectedClassId!,

                        RegisteredDate =
                            registeredAt
                    };


                db.StudentClasses
                    .Add(
                        studentClass
                    );
            }


            // ==================================================
            // SAVE STUDENT CLASS FIRST
            // ==================================================

            await db.SaveChangesAsync();



            // ==================================================
            // BACKFILL RECENT TIMETABLE CHANGES
            // ==================================================

            await BackfillRecentTimetableNotificationsAsync(
                userId,
                model.SelectedClassId!,
                registeredAt
            );
        }


        // ==================================================
        // TEACHER
        // ==================================================

        else if (
            user.Position ==
            "Teacher"
        )
        {
            List<TeacherCourse> oldRecords =
                await db.TeacherCourses
                    .Where(tc =>
                        tc.UserId ==
                        userId
                    )
                    .ToListAsync();


            db.TeacherCourses
                .RemoveRange(
                    oldRecords
                );


            await db.SaveChangesAsync();



            foreach (
                string courseId
                in model.SelectedCourseIds
                    ?? new List<string>()
            )
            {
                TeacherCourse teacherCourse =
                    new()
                    {
                        TeacherCourseId =
                            await FormattedIdGenerator
                                .NextAsync(
                                    db.TeacherCourses,
                                    tc =>
                                        tc.TeacherCourseId,
                                    "TCR"
                                ),

                        UserId =
                            userId,

                        CourseId =
                            courseId,

                        RegisteredDate =
                            DateTime.Now
                    };


                db.TeacherCourses
                    .Add(
                        teacherCourse
                    );


                // Save each record to make sure
                // generated TCR ID stays unique.

                await db.SaveChangesAsync();
            }
        }


        // ==================================================
        // FINAL SAVE
        // ==================================================

        await db.SaveChangesAsync();



        TempData["Message"] =
            "Registration saved successfully.";


        return RedirectToAction(
            "Index",
            "MyProfile"
        );
    }

    // ==================================================
    // BACKFILL RECENT TIMETABLE NOTIFICATIONS
    // ==================================================

    private async Task
        BackfillRecentTimetableNotificationsAsync(
            string userId,
            string classId,
            DateTime registeredAt)
    {
        // ==================================================
        // ACTIVE SEMESTER
        // ==================================================

        AcademicTerm? activeTerm =
            await db.AcademicTerms
                .AsNoTracking()
                .Where(t =>
                    t.Status ==
                    "Active"
                )
                .OrderBy(t =>
                    t.StartDate
                )
                .FirstOrDefaultAsync();


        if (
            activeTerm ==
            null
        )
        {
            return;
        }

        // ==================================================
        // TIMETABLES FOR THIS CLASS
        // ==================================================

        List<string> timetableIds =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.TermId ==
                        activeTerm.TermId &&
                    t.ClassId ==
                        classId
                )
                .Select(t =>
                    t.TimetableId
                )
                .ToListAsync();


        if (
            timetableIds.Count ==
            0
        )
        {
            return;
        }

        // ==================================================
        // LAST 7 DAYS
        // ==================================================

        DateTime notificationCutOff =
            DateTime.Now
                .AddDays(
                    -7
                );

        // ==================================================
        // FIND EXISTING NOTIFICATIONS
        //
        // TimetableController already created
        // the same notification for Teacher
        // and Students who were in the class
        // when the timetable was changed.
        //
        // We use one of those as the source.
        // ==================================================

        List<TimetableNotification> sourceNotifications =
            await db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>

                    // Must belong to somebody.
                    // Ignore global Admin notification.
                    n.UserId != null &&

                    // Do not copy student's own record.
                    n.UserId != userId &&

                    // Active semester only.
                    n.TermId ==
                        activeTerm.TermId &&

                    // Must have a timetable.
                    n.TimetableId != null &&

                    // Must belong to this Student's class.
                    timetableIds.Contains(
                        n.TimetableId
                    ) &&

                    // Recent notification only.
                    n.CreatedAt >=
                        notificationCutOff &&

                    // If this notification points to
                    // an actual lesson date, only include
                    // lessons on or after the Student joined.
                    (
                        !n.OccurrenceDate.HasValue ||

                        n.OccurrenceDate
                            .Value
                            .Date >=
                        registeredAt.Date
                    )
                )
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .ToListAsync();


        if (
            sourceNotifications.Count ==
            0
        )
        {
            return;
        }

        // ==================================================
        // REMOVE DUPLICATES
        //
        // Same notification can exist for:
        //
        // Teacher
        // Student A
        // Student B
        //
        // New Student only needs ONE.
        // ==================================================

        List<TimetableNotification> uniqueChanges =
            sourceNotifications
                .GroupBy(n =>
                    new
                    {
                        n.TimetableId,OccurrenceDate =n.OccurrenceDate.HasValue? n.OccurrenceDate.Value.Date: (DateTime?)null, n.Type,n.Message
                    }
                )
                .Select(g =>
                    g.OrderByDescending(n =>
                        n.CreatedAt
                    )
                    .First()
                )
                .OrderBy(n =>
                    n.CreatedAt
                )
                .ToList();



        // ==================================================
        // COPY NOTIFICATIONS TO CURRENT STUDENT
        // ==================================================

        foreach (
            TimetableNotification source
            in uniqueChanges
        )
        {
            // ==================================================
            // DON'T CREATE DUPLICATE
            // ==================================================

            bool alreadyExists =
                await db.TimetableNotifications
                    .AsNoTracking()
                    .AnyAsync(n =>

                        n.UserId ==
                            userId &&

                        n.TimetableId ==
                            source.TimetableId &&

                        n.OccurrenceDate ==
                            source.OccurrenceDate &&

                        n.Type ==
                            source.Type &&

                        n.Message ==
                            source.Message
                    );

            if (
                alreadyExists
            )
            {
                continue;
            }

            // ==================================================
            // CREATE COPY FOR NEW STUDENT
            // ==================================================

            TimetableNotification notification =
                new()
                {
                    NotificationId =
                        await FormattedIdGenerator
                            .NextAsync(
                                db.TimetableNotifications,
                                n =>
                                    n.NotificationId,
                                "NTF"
                            ),
                    UserId =
                        userId,

                    Type =
                        source.Type,

                    Message =
                        source.Message,

                    TermId =
                        source.TermId,

                    TimetableId =
                        source.TimetableId,

                    OccurrenceDate =
                        source.OccurrenceDate,

                    IsRead =
                        false,

                    // Keep original timetable
                    // change date/time.
                    CreatedAt =
                        source.CreatedAt
                };

            db.TimetableNotifications
                .Add(
                    notification
                );

            // Save immediately so the next generated
            // NTF ID stays unique.

            await db.SaveChangesAsync();
        }
    }



    // ==================================================
    // GENERATE STUDENT ID
    // ==================================================

    private async Task<string>
        GenerateStudentIdAsync()
    {
        int year =
            DateTime.Now.Year;

        string prefix =
            $"S{year}";

        int countThisYear =
            await db.StudentClasses
                .CountAsync(sc =>
                    sc.StudentId
                        .StartsWith(
                            prefix
                        )
                );


        return
            $"{prefix}" + $"{(countThisYear + 1):D3}";
    }



    // ==================================================
    // CURRENT USER ID
    // ==================================================

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}