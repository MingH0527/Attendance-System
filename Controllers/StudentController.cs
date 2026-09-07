using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebAndMobile.Filters;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize]
public class StudentController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment environment;

    public StudentController(
        DB db,
        IWebHostEnvironment environment)
    {
        this.db = db;
        this.environment = environment;
    }

    // ==============================
    // STUDENT HOME
    // ==============================

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Index()
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }


        // ==========================================
        // CURRENT STUDENT USER
        // ==========================================

        User? currentUser =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.UserId == studentId
                );

        ViewBag.StudentUsername =
            currentUser?.Username ?? "Student";

        ViewBag.StudentAvatar =
            currentUser?.Avatar ?? "";


        // ==========================================
        // STUDENT PROFILE
        // ==========================================

        StudentClass? studentClass =
            await db.StudentClasses
                .AsNoTracking()
                .Include(sc => sc.ClassGroup)
                .Include(sc => sc.Program)
                .FirstOrDefaultAsync(
                    sc => sc.UserId == studentId
                );

        bool profileCompleted =
            studentClass != null;

        ViewBag.ProfileCompleted =
            profileCompleted;

        ViewBag.ProgramName =
            studentClass?.Program?.ProgramName
            ?? "Not Registered";

        ViewBag.ClassName =
            studentClass?.ClassGroup?.ClassName
            ?? "Not Registered";


        // ==========================================
        // TODAY'S CLASSES
        // ==========================================

        List<Timetable> todayTimetable =
            new();

        if (studentClass != null)
        {
            DayOfWeek today =
                DateTime.Today.DayOfWeek;

            todayTimetable =
                await db.Timetables
                    .AsNoTracking()
                    .Where(t =>
                        t.ClassId ==
                            studentClass.ClassId &&

                        t.DayOfWeek ==
                            today &&

                        t.Term != null &&

                        t.Term.Status ==
                            "Active"
                    )
                    .Include(t => t.Course)
                    .Include(t => t.Classroom)
                    .Include(t => t.Teacher)
                    .OrderBy(t => t.StartTime)
                    .ToListAsync();
        }

        ViewBag.TodayTimetable =
            todayTimetable;

        ViewBag.TodayClassCount =
            todayTimetable.Count;


        // ==========================================
        // PENDING LEAVE
        // ==========================================

        ViewBag.PendingLeaveCount =
            await db.LeaveApplications
                .AsNoTracking()
                .CountAsync(l =>
                    l.StudentId == studentId &&
                    l.Status == "Pending"
                );


        // ==========================================
        // UNREAD CHAT
        // ==========================================

        ViewBag.UnreadMessageCount =
            await db.ChatMessages
                .AsNoTracking()
                .CountAsync(m =>
                    m.ReceiverId == studentId &&
                    !m.IsRead
                );


        return View("Student");
    }

    // ==============================
    // STUDENT TIMETABLE
    // ==============================

    [HttpGet]
    [Authorize(Roles = "Student")]
    [RequireStudentProfile]
    public async Task<IActionResult> Timetable()
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        StudentClass? studentClass =
            await db.StudentClasses
                .AsNoTracking()
                .Include(sc => sc.ClassGroup)
                .Include(sc => sc.Program)
                .FirstOrDefaultAsync(
                    sc => sc.UserId == studentId
                );

        if (studentClass == null)
        {
            ViewBag.ClassName =
                "Not Assigned";

            ViewBag.ProgramName =
                "Not Assigned";

            ViewBag.TermName =
                "No Active Semester";

            return View(
                new List<Timetable>()
            );
        }

        List<Timetable> timetable =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId == studentClass.ClassId &&
                    t.Term != null &&
                    t.Term.Status == "Active"
                )
                .Include(t => t.Term)
                .Include(t => t.Course)
                .Include(t => t.Classroom)
                .Include(t => t.Teacher)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartTime)
                .ToListAsync();

        ViewBag.ClassName =
            studentClass.ClassGroup?.ClassName
            ?? studentClass.ClassId;

        ViewBag.ProgramName =
            studentClass.Program?.ProgramName
            ?? studentClass.ProgramId;

        ViewBag.TermName =
            timetable
                .FirstOrDefault()?
                .Term?
                .Name
            ?? "No Active Semester";

        var activeTermIds =
            timetable
                .Where(t => t.TermId != null)
                .Select(t => t.TermId!)
                .Distinct()
                .ToList();

        ViewBag.TimetableNotifications =
            await db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>
                    n.UserId == studentId &&
                    n.TermId != null &&
                    activeTermIds.Contains(n.TermId)
                )
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .ToListAsync();

        return View(timetable);
    }

    // ==============================
    // STUDENT ATTENDANCE
    // ==============================

    [HttpGet]
    [Authorize(Roles = "Student")]
    [RequireStudentProfile]
    public async Task<IActionResult> Attendance()
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        StudentClass? studentClass =
            await db.StudentClasses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    sc => sc.UserId == studentId
                );

        if (studentClass == null)
        {
            return RedirectToAction(
                nameof(Index)
            );
        }

        DateTime now =
            DateTime.Now;

        DateTime today =
            DateTime.Today;

        StudentAttendanceViewModel model =
            new();

        // ==============================
        // TODAY'S TIMETABLE
        // ==============================

        List<Timetable> todayTimetables =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId == studentClass.ClassId &&
                    t.DayOfWeek == today.DayOfWeek &&
                    t.Term != null &&
                    t.Term.Status == "Active"
                )
                .Include(t => t.Course)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

        bool approvedLeaveToday =
            await db.LeaveApplications
                .AsNoTracking()
                .AnyAsync(l =>
                    l.StudentId == studentId &&
                    l.Status == "Approved" &&
                    today >= l.StartDate.Date &&
                    today <= l.EndDate.Date
                );

        foreach (
            Timetable timetable
            in todayTimetables)
        {
            AttendanceSession? session =
                await db.AttendanceSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.TimetableId ==
                            timetable.TimetableId &&
                        s.SessionDate ==
                            today
                    );

            string status =
                "Not Open";

            if (session != null)
            {
                AttendanceRecord? record =
                    await db.AttendanceRecords
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r =>
                            r.AttendanceSessionId ==
                                session.AttendanceSessionId &&
                            r.StudentClassId ==
                                studentClass.StudentClassId
                        );

                if (
                    record != null &&
                    !record.IsRemoved
                )
                {
                    status =
                        "Present";
                }
                else if (
                    approvedLeaveToday
                )
                {
                    status =
                        "Leave";
                }
                else
                {
                    DateTime classEnd =
                        today.Add(
                            timetable.EndTime
                        );

                    if (
                        now < classEnd
                    )
                    {
                        status =
                            "Not Marked";
                    }
                    else
                    {
                        status =
                            "Absent";
                    }
                }
            }

            model.TodayAttendance.Add(
                new TodayAttendanceItemViewModel
                {
                    CourseId =
                        timetable.CourseId,

                    CourseCode =
                        timetable.Course?.CourseCode
                        ?? "",

                    CourseName =
                        timetable.Course?.CourseName
                        ?? timetable.CourseId,

                    StartTime =
                        timetable.StartTime,

                    EndTime =
                        timetable.EndTime,

                    Status =
                        status
                }
            );
        }

        // ==============================
        // OVERALL ATTENDANCE
        // ==============================

        var programCourses =
            await db.ProgramCourses
                .AsNoTracking()
                .Where(pc =>
                    pc.ProgramId ==
                        studentClass.ProgramId
                )
                .Include(pc => pc.Course)
                .ToListAsync();

        foreach (
            var programCourse
            in programCourses)
        {
            if (
                programCourse.Course ==
                null
            )
            {
                continue;
            }

            string courseId =
                programCourse
                    .Course
                    .CourseId;

            List<Timetable> subjectTimetables =
                await db.Timetables
                    .AsNoTracking()
                    .Where(t =>
                        t.ClassId ==
                            studentClass.ClassId &&
                        t.CourseId ==
                            courseId &&
                        t.Term != null &&
                        t.Term.Status ==
                            "Active"
                    )
                    .ToListAsync();

            List<string> timetableIds =
                subjectTimetables
                    .Select(t =>
                        t.TimetableId
                    )
                    .ToList();

            List<AttendanceSession> sessions =
                await db.AttendanceSessions
                    .AsNoTracking()
                    .Where(s =>
                        timetableIds.Contains(
                            s.TimetableId
                        ) &&
                        s.SessionDate <=
                            today
                    )
                    .OrderBy(s =>
                        s.SessionDate
                    )
                    .ToListAsync();

            int present = 0;
            int absent = 0;
            int leave = 0;

            foreach (
                AttendanceSession session
                in sessions)
            {
                AttendanceRecord? record =
                    await db.AttendanceRecords
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r =>
                            r.AttendanceSessionId ==
                                session.AttendanceSessionId &&
                            r.StudentClassId ==
                                studentClass.StudentClassId
                        );

                // ==============================
                // PRESENT
                // ==============================

                if (
                    record != null &&
                    !record.IsRemoved
                )
                {
                    present++;
                    continue;
                }

                Timetable? sessionTimetable =
                    subjectTimetables
                        .FirstOrDefault(t =>
                            t.TimetableId ==
                                session.TimetableId
                        );

                if (
                    sessionTimetable ==
                    null
                )
                {
                    continue;
                }

                // ==============================
                // LEAVE
                // ==============================

                bool approvedLeave =
                    await db.LeaveApplications
                        .AsNoTracking()
                        .AnyAsync(l =>
                            l.StudentId ==
                                studentId &&
                            l.Status ==
                                "Approved" &&
                            session.SessionDate.Date >=
                                l.StartDate.Date &&
                            session.SessionDate.Date <=
                                l.EndDate.Date
                        );

                if (
                    approvedLeave
                )
                {
                    leave++;
                    continue;
                }

                // ==============================
                // CLASS END TIME
                // ==============================

                DateTime classEnd =
                    session.SessionDate.Date
                        .Add(
                            sessionTimetable.EndTime
                        );

                // Class not ended yet
                // Do not count absent
                if (
                    now < classEnd
                )
                {
                    continue;
                }

                // Class already ended
                absent++;
            }

            // ==============================
            // ATTENDANCE PERCENTAGE
            // ==============================

            // Each absent deducts 2%
            const double AbsentPenalty =
                2.0;

            double percentage =
                100 -
                (
                    absent *
                    AbsentPenalty
                );

            if (
                percentage < 0
            )
            {
                percentage =
                    0;
            }

            model.Subjects.Add(
                new SubjectAttendanceViewModel
                {
                    CourseId =
                        programCourse
                            .Course
                            .CourseId,

                    CourseCode =
                        programCourse
                            .Course
                            .CourseCode
                        ?? "",

                    CourseName =
                        programCourse
                            .Course
                            .CourseName,

                    PresentCount =
                        present,

                    AbsentCount =
                        absent,

                    LeaveCount =
                        leave,

                    Percentage =
                        percentage
                }
            );
        }

        model.Subjects =
            model.Subjects
                .OrderBy(s =>
                    s.CourseName
                )
                .ToList();

        return View(model);
    }

    // ==============================
    // SUBMIT ATTENDANCE CODE
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Student")]
    [RequireStudentProfile]
    public async Task<IActionResult>
        SubmitAttendanceCode(
            string code)
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (
            string.IsNullOrWhiteSpace(
                studentId
            )
        )
        {
            return Unauthorized();
        }

        if (
            string.IsNullOrWhiteSpace(
                code
            )
        )
        {
            TempData["AttendanceError"] =
                "Please enter the attendance code.";

            return RedirectToAction(
                nameof(Attendance)
            );
        }

        code =
            code.Trim();

        if (
            code.Length != 6 ||
            !code.All(char.IsDigit)
        )
        {
            TempData["AttendanceError"] =
                "Attendance code must contain 6 digits.";

            return RedirectToAction(
                nameof(Attendance)
            );
        }

        StudentClass? studentClass =
            await db.StudentClasses
                .FirstOrDefaultAsync(
                    sc =>
                        sc.UserId ==
                        studentId
                );

        if (
            studentClass ==
            null
        )
        {
            TempData["AttendanceError"] =
                "Student class was not found.";

            return RedirectToAction(
                nameof(Attendance)
            );
        }

        DateTime now =
            DateTime.Now;

        AttendanceSession? session =
            await db.AttendanceSessions
                .Include(s =>
                    s.Timetable
                )
                .ThenInclude(t =>
                    t!.Course
                )
                .FirstOrDefaultAsync(s =>
                    s.Code ==
                        code &&
                    s.SessionDate ==
                        now.Date &&
                    s.IsActive &&
                    s.ExpiresAt >
                        now &&
                    s.Timetable !=
                        null &&
                    s.Timetable.ClassId ==
                        studentClass.ClassId
                );

        if (
            session ==
            null
        )
        {
            TempData["AttendanceError"] =
                "Invalid or expired attendance code.";

            return RedirectToAction(
                nameof(Attendance)
            );
        }

        bool alreadySubmitted =
            await db.AttendanceRecords
                .AnyAsync(r =>
                    r.AttendanceSessionId ==
                        session.AttendanceSessionId &&
                    r.StudentClassId ==
                        studentClass.StudentClassId
                );

        if (
            alreadySubmitted
        )
        {
            TempData["AttendanceError"] =
                "Attendance has already been submitted.";

            return RedirectToAction(
                nameof(Attendance)
            );
        }

        AttendanceRecord record =
            new()
            {
                AttendanceRecordId =
                    await FormattedIdGenerator
                        .NextAsync(
                            db.AttendanceRecords,
                            r =>
                                r.AttendanceRecordId,
                            "ATR"
                        ),

                AttendanceSessionId =
                    session
                        .AttendanceSessionId,

                StudentClassId =
                    studentClass
                        .StudentClassId,

                Status =
                    "Present",

                SubmittedAt =
                    now,

                IsRemoved =
                    false
            };

        db.AttendanceRecords.Add(
            record
        );

        await db.SaveChangesAsync();

        TempData["AttendanceSuccess"] =
            "Attendance submitted successfully.";

        return RedirectToAction(
            nameof(Attendance)
        );
    }

    // ==============================
    // ATTENDANCE DETAILS
    // ==============================

    [HttpGet]
    [Authorize(Roles = "Student")]
    [RequireStudentProfile]
    public async Task<IActionResult>
        AttendanceDetails(
            string courseId)
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (
            string.IsNullOrWhiteSpace(
                studentId
            )
        )
        {
            return Unauthorized();
        }

        StudentClass? studentClass =
            await db.StudentClasses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    sc =>
                        sc.UserId ==
                        studentId
                );

        if (
            studentClass ==
            null
        )
        {
            return NotFound();
        }

        Course? course =
            await db.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.CourseId ==
                        courseId
                );

        if (
            course ==
            null
        )
        {
            return NotFound();
        }

        List<Timetable> timetables =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId ==
                        studentClass.ClassId &&
                    t.CourseId ==
                        courseId &&
                    t.Term != null &&
                    t.Term.Status ==
                        "Active"
                )
                .ToListAsync();

        List<string> timetableIds =
            timetables
                .Select(t =>
                    t.TimetableId
                )
                .ToList();

        DateTime now =
            DateTime.Now;

        DateTime today =
            DateTime.Today;

        List<AttendanceSession> sessions =
            await db.AttendanceSessions
                .AsNoTracking()
                .Where(s =>
                    timetableIds.Contains(
                        s.TimetableId
                    ) &&
                    s.SessionDate <=
                        today
                )
                .OrderBy(s =>
                    s.SessionDate
                )
                .ToListAsync();

        AttendanceDetailsViewModel model =
            new()
            {
                CourseId =
                    course.CourseId,

                CourseCode =
                    course.CourseCode
                    ?? "",

                CourseName =
                    course.CourseName
            };

        foreach (
            AttendanceSession session
            in sessions)
        {
            Timetable? timetable =
                timetables
                    .FirstOrDefault(t =>
                        t.TimetableId ==
                            session.TimetableId
                    );

            if (
                timetable ==
                null
            )
            {
                continue;
            }

            AttendanceRecord? record =
                await db.AttendanceRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.AttendanceSessionId ==
                            session.AttendanceSessionId &&
                        r.StudentClassId ==
                            studentClass.StudentClassId
                    );

            string status;
            string? description =
                null;

            // ==============================
            // PRESENT
            // ==============================

            if (
                record != null &&
                !record.IsRemoved
            )
            {
                status =
                    "Present";
            }
            else
            {
                // ==============================
                // LEAVE
                // ==============================

                LeaveApplication? approvedLeave =
                    await db.LeaveApplications
                        .AsNoTracking()
                        .FirstOrDefaultAsync(l =>
                            l.StudentId ==
                                studentId &&
                            l.Status ==
                                "Approved" &&
                            session.SessionDate.Date >=
                                l.StartDate.Date &&
                            session.SessionDate.Date <=
                                l.EndDate.Date
                        );

                if (
                    approvedLeave !=
                    null
                )
                {
                    status =
                        "Leave";

                    description =
                        approvedLeave
                            .LeaveType;
                }
                else
                {
                    // ==============================
                    // CLASS END TIME
                    // ==============================

                    DateTime classEnd =
                        session.SessionDate.Date
                            .Add(
                                timetable.EndTime
                            );

                    // Class not finished
                    // Do not show absent yet
                    if (
                        now < classEnd
                    )
                    {
                        continue;
                    }

                    // ==============================
                    // ABSENT
                    // ==============================

                    status =
                        "Absent";

                    if (
                        record != null &&
                        record.IsRemoved
                    )
                    {
                        description =
                            record.Description;
                    }
                }
            }

            // ==============================
            // COUNTS
            // ==============================

            if (
                status ==
                "Present"
            )
            {
                model.PresentCount++;
            }
            else if (
                status ==
                "Absent"
            )
            {
                model.AbsentCount++;
            }
            else if (
                status ==
                "Leave"
            )
            {
                model.LeaveCount++;
            }

            // ==============================
            // HISTORY
            // ==============================

            model.History.Add(
                new AttendanceHistoryItemViewModel
                {
                    Date =
                        session.SessionDate,

                    StartTime =
                        timetable.StartTime,

                    EndTime =
                        timetable.EndTime,

                    Status =
                        status,

                    Description =
                        description
                }
            );
        }

        // ==============================
        // ATTENDANCE PERCENTAGE
        // ==============================

        // Each absent deducts 2%
        const double AbsentPenalty =
            2.0;

        model.Percentage =
            100 -
            (
                model.AbsentCount *
                AbsentPenalty
            );

        if (
            model.Percentage < 0
        )
        {
            model.Percentage =
                0;
        }

        // ==============================
        // DONUT CHART
        // ==============================

        int total =
            model.PresentCount +
            model.AbsentCount +
            model.LeaveCount;

        if (
            total > 0
        )
        {
            model.PresentPercent =
                model.PresentCount *
                100.0 /
                total;

            model.AbsentPercent =
                model.AbsentCount *
                100.0 /
                total;

            model.LeavePercent =
                model.LeaveCount *
                100.0 /
                total;
        }

        return View(model);
    }

    // ==============================
    // GET ACTIVE SEMESTER
    // ==============================

    private async Task<AcademicTerm?>
        GetActiveLeaveTermAsync()
    {
        DateTime today =
            DateTime.Today;

        return await db.AcademicTerms
            .AsNoTracking()
            .Where(t =>
                t.Status == "Active" &&
                t.StartDate.Date <= today &&
                t.EndDate.Date >= today
            )
            .OrderBy(t =>
                t.StartDate
            )
            .FirstOrDefaultAsync();
    }

    // ==============================
    // APPLY LEAVE - GET
    // ==============================

    [HttpGet]
    [RequireStudentProfile]
    public async Task<IActionResult> ApplyLeave()
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (
            string.IsNullOrWhiteSpace(
                studentId
            )
        )
        {
            return Unauthorized();
        }

        AcademicTerm? activeTerm =
            await GetActiveLeaveTermAsync();

        ViewBag.ActiveLeaveTerm =
            activeTerm;

        DateTime today =
            DateTime.Today;

        ApplyLeaveViewModel model =
            new()
            {
                StartDate =
                    today,

                EndDate =
                    today,

                LeaveHistory =
                    await db.LeaveApplications
                        .Where(l =>
                            l.StudentId ==
                                studentId
                        )
                        .OrderByDescending(
                            l => l.AppliedAt
                        )
                        .ToListAsync()
            };

        return View(model);
    }

    // ==============================
    // APPLY LEAVE - POST
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireStudentProfile]
    public async Task<IActionResult> ApplyLeave(
        ApplyLeaveViewModel model)
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (
            string.IsNullOrWhiteSpace(
                studentId
            )
        )
        {
            return Unauthorized();
        }

        // ==============================
        // DATE VALIDATION
        // ==============================

        if (
            model.StartDate.Date <
            DateTime.Today
        )
        {
            ModelState.AddModelError(
                nameof(model.StartDate),
                "Start date cannot be in the past."
            );
        }

        if (
            model.EndDate.Date <
            model.StartDate.Date
        )
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "End date cannot be earlier than start date."
            );
        }

        if (
            model.EndDate.Date >
            model.StartDate.Date
                .AddDays(6)
        )
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "Leave application cannot exceed 7 days."
            );
        }

        // ==============================
        // ACTIVE SEMESTER VALIDATION
        // ==============================

        AcademicTerm? activeTerm =
            await GetActiveLeaveTermAsync();

        if (
            activeTerm ==
            null
        )
        {
            ModelState.AddModelError(
                nameof(model.StartDate),
                "There is currently no active semester."
            );
        }
        else
        {
            if (
                model.StartDate.Date <
                    activeTerm.StartDate.Date ||
                model.StartDate.Date >
                    activeTerm.EndDate.Date
            )
            {
                ModelState.AddModelError(
                    nameof(model.StartDate),
                    "Start date must be within the current semester."
                );
            }

            if (
                model.EndDate.Date <
                    activeTerm.StartDate.Date ||
                model.EndDate.Date >
                    activeTerm.EndDate.Date
            )
            {
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "End date must be within the current semester."
                );
            }
        }

        // ==============================
        // OVERLAPPING LEAVE VALIDATION
        // ==============================

        bool hasOverlappingLeave =
            await db.LeaveApplications
                .AsNoTracking()
                .AnyAsync(l =>
                    l.StudentId ==
                        studentId &&
                    (
                        l.Status == "Pending" ||
                        l.Status == "Approved"
                    ) &&
                    model.StartDate.Date <=
                        l.EndDate.Date &&
                    model.EndDate.Date >=
                        l.StartDate.Date
                );

        if (
            hasOverlappingLeave
        )
        {
            ModelState.AddModelError(
                nameof(model.StartDate),
                "You already have a pending or approved leave application for the selected date range."
            );
        }


        // ==============================
        // FILE VALIDATION
        // ==============================

        string? documentName =
            null;

        if (
            model.SupportingDocument != null &&
            model.SupportingDocument.Length >
            0
        )
        {
            string extension =
                Path.GetExtension(
                    model.SupportingDocument
                        .FileName
                )
                .ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".pdf",
                ".jpg",
                ".jpeg",
                ".png"
            };

            if (
                !allowedExtensions.Contains(
                    extension
                )
            )
            {
                ModelState.AddModelError(
                    nameof(model.SupportingDocument),
                    "Only PDF, JPG, JPEG and PNG files are allowed."
                );
            }

            if (
                model.SupportingDocument.Length >
                5 * 1024 * 1024
            )
            {
                ModelState.AddModelError(
                    nameof(model.SupportingDocument),
                    "File size cannot exceed 5MB."
                );
            }
        }

        // ==============================
        // VALIDATION FAILED
        // ==============================

        if (
            !ModelState.IsValid
        )
        {
            ViewBag.ActiveLeaveTerm =
                activeTerm;

            model.LeaveHistory =
                await db.LeaveApplications
                    .Where(l =>
                        l.StudentId ==
                            studentId
                    )
                    .OrderByDescending(
                        l => l.AppliedAt
                    )
                    .ToListAsync();

            return View(model);
        }

        // ==============================
        // SAVE DOCUMENT
        // ==============================

        if (
            model.SupportingDocument != null &&
            model.SupportingDocument.Length >
            0
        )
        {
            string extension =
                Path.GetExtension(
                    model.SupportingDocument
                        .FileName
                )
                .ToLowerInvariant();

            string folder =
                Path.Combine(
                    environment.WebRootPath,
                    "uploads",
                    "leave"
                );

            Directory.CreateDirectory(
                folder
            );

            documentName =
                Guid.NewGuid()
                    .ToString() +
                extension;

            string filePath =
                Path.Combine(
                    folder,
                    documentName
                );

            using FileStream stream =
                new(
                    filePath,
                    FileMode.Create
                );

            await model
                .SupportingDocument
                .CopyToAsync(
                    stream
                );
        }

        // ==============================
        // CREATE LEAVE APPLICATION
        // ==============================

        LeaveApplication leave =
            new()
            {
                StudentId =
                    studentId,

                LeaveType =
                    model.LeaveType,

                StartDate =
                    model.StartDate.Date,

                EndDate =
                    model.EndDate.Date,

                Reason =
                    model.Reason,

                SupportingDocument =
                    documentName,

                Status =
                    "Pending",

                AppliedAt =
                    DateTime.Now
            };

        db.LeaveApplications.Add(
            leave
        );

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Leave application submitted successfully.";

        return RedirectToAction(
            nameof(ApplyLeave)
        );
    }

    // ==============================
    // CANCEL LEAVE
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelLeave(
        int id)
    {
        string? studentId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (
            string.IsNullOrWhiteSpace(
                studentId
            )
        )
        {
            return Unauthorized();
        }

        LeaveApplication? leave =
            await db.LeaveApplications
                .FirstOrDefaultAsync(l =>
                    l.LeaveId ==
                        id &&
                    l.StudentId ==
                        studentId
                );

        if (
            leave ==
            null
        )
        {
            return NotFound();
        }

        if (
            !string.Equals(
                leave.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            TempData["ErrorMessage"] =
                "Only pending leave applications can be cancelled.";

            return RedirectToAction(
                nameof(ApplyLeave)
            );
        }

        // ==============================
        // DELETE UPLOADED DOCUMENT
        // ==============================

        if (
            !string.IsNullOrWhiteSpace(
                leave.SupportingDocument
            )
        )
        {
            string filePath =
                Path.Combine(
                    environment.WebRootPath,
                    "uploads",
                    "leave",
                    leave.SupportingDocument
                );

            if (
                System.IO.File.Exists(
                    filePath
                )
            )
            {
                System.IO.File.Delete(
                    filePath
                );
            }
        }

        db.LeaveApplications.Remove(
            leave
        );

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Leave application cancelled successfully.";

        return RedirectToAction(
            nameof(ApplyLeave)
        );
    }

    // ==============================
    // LOGOUT
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme
        );

        return RedirectToAction(
            "Login",
            "Account"
        );
    }
}