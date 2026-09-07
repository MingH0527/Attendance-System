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

    public TeacherController(DB db)
    {
        this.db = db;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }


    // ==================================================
    // TEACHER HOME
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Index()
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


        await TryLinkLegacyProfileAsync(
            userId
        );


        bool hasTeacherCourse =
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == userId
                );


        if (!hasTeacherCourse)
        {
            return RedirectToAction(
                nameof(Create)
            );
        }


        ViewBag.PasskeyCount =
            await db.UserPasskeys
                .CountAsync(p =>
                    p.UserId ==
                    user.UserId
                );


        ViewBag.TimetableCount =
            await db.Timetables
                .CountAsync(t =>
                    t.TeacherId ==
                        user.UserId &&
                    (
                        t.Term == null ||
                        t.Term.Status ==
                            "Active"
                    )
                );


        return View(user);
    }


    // ==================================================
    // TEACHER TIMETABLE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Timetable(
        int? weekNumber = null)
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


        await TryLinkLegacyProfileAsync(
            userId
        );


        bool hasTeacherCourse =
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == userId
                );


        if (!hasTeacherCourse)
        {
            return RedirectToAction(
                nameof(Create)
            );
        }


        TeacherTimetableViewModel model =
            new();


        // ==================================================
        // ACTIVE SEMESTER
        // ==================================================

        AcademicTerm? activeTerm =
            await db.AcademicTerms
                .AsNoTracking()
                .Where(t =>
                    t.Status == "Active"
                )
                .OrderBy(t =>
                    t.StartDate
                )
                .FirstOrDefaultAsync();


        if (activeTerm == null)
        {
            return View(model);
        }


        model.TermName =
            activeTerm.Name;


        // ==================================================
        // ACADEMIC WEEKS
        // ==================================================

        model.Weeks =
            await db.AcademicWeeks
                .AsNoTracking()
                .Where(w =>
                    w.TermId ==
                    activeTerm.TermId
                )
                .OrderBy(w =>
                    w.WeekNumber
                )
                .ToListAsync();


        // ==================================================
        // WEEK SELECTION
        //
        // 0    = Weekly Template
        // null = Current Week
        // 1... = Specific Week
        // ==================================================

        if (weekNumber == 0)
        {
            model.IsTemplate =
                true;

            model.SelectedWeekNumber =
                null;
        }
        else
        {
            AcademicWeek? selectedWeek =
                null;


            if (weekNumber.HasValue)
            {
                selectedWeek =
                    model.Weeks
                        .FirstOrDefault(w =>
                            w.WeekNumber ==
                            weekNumber.Value
                        );
            }


            if (selectedWeek == null)
            {
                DateTime today =
                    DateTime.Today;


                selectedWeek =
                    model.Weeks
                        .FirstOrDefault(w =>
                            today >=
                                w.StartDate.Date &&
                            today <=
                                w.EndDate.Date
                        );
            }


            selectedWeek ??=
                model.Weeks
                    .FirstOrDefault();


            if (selectedWeek != null)
            {
                model.IsTemplate =
                    false;

                model.SelectedWeekNumber =
                    selectedWeek.WeekNumber;

                model.WeekStart =
                    selectedWeek.StartDate.Date;

                model.WeekEnd =
                    selectedWeek.EndDate.Date;
            }
            else
            {
                model.IsTemplate =
                    true;
            }
        }


        // ==================================================
        // GET TEACHER TIMETABLE
        //
        // IMPORTANT:
        // Old timetable may have TermId = NULL.
        // New timetable uses Active TermId.
        // Both are accepted.
        // ==================================================

        List<Timetable> entries =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.TeacherId ==
                        userId &&
                    (
                        t.TermId ==
                            activeTerm.TermId ||
                        t.TermId ==
                            null
                    )
                )
                .Include(t =>
                    t.Term
                )
                .Include(t =>
                    t.Course
                )
                .Include(t =>
                    t.ClassGroup
                )
                .Include(t =>
                    t.Classroom
                )
                .OrderBy(t =>
                    t.DayOfWeek
                )
                .ThenBy(t =>
                    t.StartTime
                )
                .ToListAsync();


        // ==================================================
        // WEEKLY TEMPLATE
        // ==================================================

        if (model.IsTemplate)
        {
            foreach (
                Timetable entry
                in entries
            )
            {
                model.Items.Add(
                    new TeacherTimetableItemViewModel
                    {
                        TimetableId =
                            entry.TimetableId,

                        CourseId =
                            entry.CourseId,

                        CourseName =
                            entry.Course
                                ?.CourseName
                            ?? entry.CourseId,

                        ClassName =
                            entry.ClassGroup
                                ?.ClassName
                            ?? entry.ClassId,

                        RoomName =
                            entry.Classroom
                                ?.RoomName
                            ?? "-",

                        DayOfWeek =
                            entry.DayOfWeek,

                        OccurrenceDate =
                            null,

                        StartTime =
                            entry.StartTime,

                        EndTime =
                            entry.EndTime,

                        ClassType =
                            entry.ClassType,

                        Status =
                            "Normal",

                        ReplacementId =
                            null,

                        Reason =
                            null
                    }
                );
            }
        }


        // ==================================================
        // SPECIFIC WEEK
        // ==================================================

        else if (
            model.WeekStart != null &&
            model.WeekEnd != null
        )
        {
            DateTime weekStart =
                model.WeekStart.Value.Date;

            DateTime weekEnd =
                model.WeekEnd.Value.Date;


            List<string> timetableIds =
                entries
                    .Select(t =>
                        t.TimetableId
                    )
                    .ToList();


            // ==================================================
            // TIMETABLE EXCEPTIONS
            // ==================================================

            List<TimetableException> exceptions =
                await db.TimetableExceptions
                    .AsNoTracking()
                    .Where(e =>
                        timetableIds.Contains(
                            e.TimetableId
                        ) &&
                        e.OccurrenceDate.Date >=
                            weekStart &&
                        e.OccurrenceDate.Date <=
                            weekEnd
                    )
                    .ToListAsync();


            // ==================================================
            // REPLACEMENT CLASSES
            // ==================================================

            List<ReplacementSession> replacements =
                await db.ReplacementSessions
                    .AsNoTracking()
                    .Where(r =>
                        r.TeacherId ==
                            userId &&
                        r.ReplacementDate.Date >=
                            weekStart &&
                        r.ReplacementDate.Date <=
                            weekEnd
                    )
                    .Include(r =>
                        r.SourceTimetable
                    )
                    .ThenInclude(t =>
                        t!.Course
                    )
                    .Include(r =>
                        r.SourceTimetable
                    )
                    .ThenInclude(t =>
                        t!.ClassGroup
                    )
                    .ToListAsync();


            // ==================================================
            // SPECIAL CLASSROOM IDS
            // ==================================================

            List<string> specialRoomIds =
                exceptions
                    .Where(e =>
                        !string.IsNullOrWhiteSpace(
                            e.OverrideClassroomId
                        )
                    )
                    .Select(e =>
                        e.OverrideClassroomId!
                    )
                    .Concat(
                        replacements
                            .Where(r =>
                                !string.IsNullOrWhiteSpace(
                                    r.ClassroomId
                                )
                            )
                            .Select(r =>
                                r.ClassroomId!
                            )
                    )
                    .Distinct()
                    .ToList();


            Dictionary<string, string>
                roomNames =
                    await db.Classrooms
                        .AsNoTracking()
                        .Where(c =>
                            specialRoomIds.Contains(
                                c.ClassroomId
                            )
                        )
                        .ToDictionaryAsync(
                            c => c.ClassroomId,
                            c => c.RoomName
                        );


            // ==================================================
            // NORMAL / CANCELLED CLASSES
            // ==================================================

            foreach (
                Timetable entry
                in entries
            )
            {
                DateTime occurrenceDate =
                    GetDateForDay(
                        weekStart,
                        entry.DayOfWeek
                    );


                if (
                    occurrenceDate <
                        weekStart ||
                    occurrenceDate >
                        weekEnd
                )
                {
                    continue;
                }


                TimetableException? exception =
                    exceptions
                        .FirstOrDefault(e =>
                            e.TimetableId ==
                                entry.TimetableId &&
                            e.OccurrenceDate.Date ==
                                occurrenceDate.Date &&
                            !IsPendingDecision(
                                e.Decision
                            )
                        );


                bool cancelled =
                    exception != null &&
                    IsCancelledValue(
                        exception.Decision
                    );


                TimeSpan actualStart =
                    exception
                        ?.OverrideStartTime
                    ?? entry.StartTime;


                TimeSpan actualEnd =
                    exception
                        ?.OverrideEndTime
                    ?? entry.EndTime;


                string roomName =
                    entry.Classroom
                        ?.RoomName
                    ?? "-";


                if (
                    exception != null &&
                    !string.IsNullOrWhiteSpace(
                        exception.OverrideClassroomId
                    ) &&
                    roomNames.TryGetValue(
                        exception.OverrideClassroomId!,
                        out string? overrideRoom
                    )
                )
                {
                    roomName =
                        overrideRoom;
                }


                model.Items.Add(
                    new TeacherTimetableItemViewModel
                    {
                        TimetableId =
                            entry.TimetableId,

                        CourseId =
                            entry.CourseId,

                        CourseName =
                            entry.Course
                                ?.CourseName
                            ?? entry.CourseId,

                        ClassName =
                            entry.ClassGroup
                                ?.ClassName
                            ?? entry.ClassId,

                        RoomName =
                            roomName,

                        DayOfWeek =
                            occurrenceDate.DayOfWeek,

                        OccurrenceDate =
                            occurrenceDate,

                        StartTime =
                            actualStart,

                        EndTime =
                            actualEnd,

                        ClassType =
                            entry.ClassType,

                        Status =
                            cancelled
                                ? "Cancelled"
                                : "Normal",

                        ReplacementId =
                            null,

                        Reason =
                            exception?.Reason
                    }
                );
            }


            // ==================================================
            // REPLACEMENT CLASSES
            // ==================================================

            foreach (
                ReplacementSession replacement
                in replacements
            )
            {
                if (
                    IsCancelledValue(
                        replacement.Status
                    ) ||
                    string.Equals(
                        replacement.Status,
                        "Rejected",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }


                Timetable? source =
                    replacement.SourceTimetable;


                if (source == null)
                {
                    continue;
                }


                string roomName =
                    "-";


                if (
                    !string.IsNullOrWhiteSpace(
                        replacement.ClassroomId
                    ) &&
                    roomNames.TryGetValue(
                        replacement.ClassroomId!,
                        out string? replacementRoom
                    )
                )
                {
                    roomName =
                        replacementRoom;
                }


                model.Items.Add(
                    new TeacherTimetableItemViewModel
                    {
                        TimetableId =
                            source.TimetableId,

                        ReplacementId =
                            replacement.ReplacementId,

                        CourseId =
                            source.CourseId,

                        CourseName =
                            source.Course
                                ?.CourseName
                            ?? source.CourseId,

                        ClassName =
                            source.ClassGroup
                                ?.ClassName
                            ?? source.ClassId,

                        RoomName =
                            roomName,

                        DayOfWeek =
                            replacement
                                .ReplacementDate
                                .DayOfWeek,

                        OccurrenceDate =
                            replacement
                                .ReplacementDate
                                .Date,

                        StartTime =
                            replacement.StartTime,

                        EndTime =
                            replacement.EndTime,

                        ClassType =
                            replacement.TargetClassType,

                        Status =
                            "Replacement",

                        Reason =
                            replacement.Reason
                    }
                );
            }
        }


        // ==================================================
        // NOTIFICATIONS
        // ==================================================

        model.Notifications =
            await db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>
                    n.UserId ==
                        userId &&
                    n.TermId ==
                        activeTerm.TermId
                )
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .Take(10)
                .ToListAsync();


        model.Items =
            model.Items
                .OrderBy(i =>
                    GetDayOrder(
                        i.DayOfWeek
                    )
                )
                .ThenBy(i =>
                    i.StartTime
                )
                .ToList();


        return View(model);
    }


    // ==================================================
    // ATTENDANCE CODE PAGE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> AttendanceCode()
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


        await TryLinkLegacyProfileAsync(
            userId
        );


        if (
            !await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == userId
                )
        )
        {
            return RedirectToAction(
                nameof(Create)
            );
        }


        DateTime now =
            DateTime.Now;

        DateTime today =
            DateTime.Today;


        TeacherAttendanceCodeViewModel model =
            new()
            {
                Date =
                    today
            };


        // ==================================================
        // ACTIVE SEMESTER
        // ==================================================

        AcademicTerm? activeTerm =
            await db.AcademicTerms
                .AsNoTracking()
                .Where(t =>
                    t.Status == "Active"
                )
                .OrderBy(t =>
                    t.StartDate
                )
                .FirstOrDefaultAsync();


        if (activeTerm == null)
        {
            return View(model);
        }


        // ==================================================
        // TODAY NORMAL CLASSES
        //
        // Also accept old TermId = NULL records.
        // ==================================================

        List<Timetable> normalClasses =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.TeacherId ==
                        userId &&
                    t.DayOfWeek ==
                        today.DayOfWeek &&
                    (
                        t.TermId ==
                            activeTerm.TermId ||
                        t.TermId ==
                            null
                    )
                )
                .Include(t =>
                    t.Course
                )
                .Include(t =>
                    t.ClassGroup
                )
                .Include(t =>
                    t.Classroom
                )
                .OrderBy(t =>
                    t.StartTime
                )
                .ToListAsync();


        List<string> timetableIds =
            normalClasses
                .Select(t =>
                    t.TimetableId
                )
                .ToList();


        // ==================================================
        // TODAY EXCEPTIONS
        // ==================================================

        List<TimetableException> exceptions =
            await db.TimetableExceptions
                .AsNoTracking()
                .Where(e =>
                    timetableIds.Contains(
                        e.TimetableId
                    ) &&
                    e.OccurrenceDate.Date ==
                        today
                )
                .ToListAsync();


        // ==================================================
        // TODAY REPLACEMENTS
        // ==================================================

        List<ReplacementSession> replacements =
            await db.ReplacementSessions
                .AsNoTracking()
                .Where(r =>
                    r.TeacherId ==
                        userId &&
                    r.ReplacementDate.Date ==
                        today
                )
                .Include(r =>
                    r.SourceTimetable
                )
                .ThenInclude(t =>
                    t!.Course
                )
                .Include(r =>
                    r.SourceTimetable
                )
                .ThenInclude(t =>
                    t!.ClassGroup
                )
                .ToListAsync();


        // ==================================================
        // ROOM IDS
        // ==================================================

        List<string> roomIds =
            exceptions
                .Where(e =>
                    !string.IsNullOrWhiteSpace(
                        e.OverrideClassroomId
                    )
                )
                .Select(e =>
                    e.OverrideClassroomId!
                )
                .Concat(
                    replacements
                        .Where(r =>
                            !string.IsNullOrWhiteSpace(
                                r.ClassroomId
                            )
                        )
                        .Select(r =>
                            r.ClassroomId!
                        )
                )
                .Distinct()
                .ToList();


        Dictionary<string, string> rooms =
            await db.Classrooms
                .AsNoTracking()
                .Where(r =>
                    roomIds.Contains(
                        r.ClassroomId
                    )
                )
                .ToDictionaryAsync(
                    r => r.ClassroomId,
                    r => r.RoomName
                );


        // ==================================================
        // NORMAL CLASSES
        // ==================================================

        foreach (
            Timetable timetable
            in normalClasses
        )
        {
            TimetableException? exception =
                exceptions
                    .FirstOrDefault(e =>
                        e.TimetableId ==
                            timetable.TimetableId &&
                        !IsPendingDecision(
                            e.Decision
                        )
                    );


            if (
                exception != null &&
                IsCancelledValue(
                    exception.Decision
                )
            )
            {
                continue;
            }


            TimeSpan startTime =
                exception
                    ?.OverrideStartTime
                ?? timetable.StartTime;


            TimeSpan endTime =
                exception
                    ?.OverrideEndTime
                ?? timetable.EndTime;


            string roomName =
                timetable.Classroom
                    ?.RoomName
                ?? "-";


            if (
                exception != null &&
                !string.IsNullOrWhiteSpace(
                    exception.OverrideClassroomId
                ) &&
                rooms.TryGetValue(
                    exception.OverrideClassroomId!,
                    out string? overrideRoom
                )
            )
            {
                roomName =
                    overrideRoom;
            }


            model.Classes.Add(
                new TeacherAttendanceCodeItemViewModel
                {
                    TimetableId =
                        timetable.TimetableId,

                    ReplacementId =
                        null,

                    CourseName =
                        timetable.Course
                            ?.CourseName
                        ?? timetable.CourseId,

                    ClassName =
                        timetable.ClassGroup
                            ?.ClassName
                        ?? timetable.ClassId,

                    RoomName =
                        roomName,

                    StartTime =
                        startTime,

                    EndTime =
                        endTime,

                    Status =
                        "Normal"
                }
            );
        }


        // ==================================================
        // REPLACEMENT CLASSES
        // ==================================================

        foreach (
            ReplacementSession replacement
            in replacements
        )
        {
            if (
                !string.Equals(
                    replacement.Status,
                    "Scheduled",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }


            Timetable? source =
                replacement.SourceTimetable;


            if (source == null)
            {
                continue;
            }


            string roomName =
                "-";


            if (
                !string.IsNullOrWhiteSpace(
                    replacement.ClassroomId
                ) &&
                rooms.TryGetValue(
                    replacement.ClassroomId!,
                    out string? replacementRoom
                )
            )
            {
                roomName =
                    replacementRoom;
            }


            model.Classes.Add(
                new TeacherAttendanceCodeItemViewModel
                {
                    TimetableId =
                        source.TimetableId,

                    ReplacementId =
                        replacement.ReplacementId,

                    CourseName =
                        source.Course
                            ?.CourseName
                        ?? source.CourseId,

                    ClassName =
                        source.ClassGroup
                            ?.ClassName
                        ?? source.ClassId,

                    RoomName =
                        roomName,

                    StartTime =
                        replacement.StartTime,

                    EndTime =
                        replacement.EndTime,

                    Status =
                        "Replacement"
                }
            );
        }


        // ==================================================
        // ACTIVE ATTENDANCE CODES
        // ==================================================

        List<string> allTimetableIds =
            model.Classes
                .Select(c =>
                    c.TimetableId
                )
                .Distinct()
                .ToList();


        List<AttendanceSession> sessions =
            await db.AttendanceSessions
                .AsNoTracking()
                .Where(s =>
                    allTimetableIds.Contains(
                        s.TimetableId
                    ) &&
                    s.SessionDate ==
                        today &&
                    s.IsActive &&
                    s.ExpiresAt >
                        now
                )
                .ToListAsync();


        // ==================================================
        // GENERATE BUTTON VALIDATION
        // ==================================================

        foreach (
            TeacherAttendanceCodeItemViewModel item
            in model.Classes
        )
        {
            AttendanceSession? session =
                sessions
                    .FirstOrDefault(s =>
                        s.TimetableId ==
                        item.TimetableId
                    );


            if (session != null)
            {
                item.ActiveCode =
                    session.Code;

                item.ExpiresAt =
                    session.ExpiresAt;

                item.CanGenerate =
                    false;

                item.DisabledReason =
                    "Code already generated.";

                continue;
            }


            if (
                now.TimeOfDay <
                item.StartTime
            )
            {
                item.CanGenerate =
                    false;

                item.DisabledReason =
                    $"Available at " +
                    $"{item.StartTime:hh\\:mm}.";

                continue;
            }


            // Class already ended
            if (
                now.TimeOfDay >=
                item.EndTime
            )
            {
                item.CanGenerate =
                    false;

                item.DisabledReason =
                    "Class has ended.";

                continue;
            }


            item.CanGenerate =
                true;

            item.DisabledReason =
                null;
        }


        model.Classes =
            model.Classes
                .OrderBy(c =>
                    c.StartTime
                )
                .ToList();


        return View(model);
    }


    // ==================================================
    // CREATE / EDIT TEACHER PROFILE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Create()
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


        await TryLinkLegacyProfileAsync(
            userId
        );


        List<string> selectedProgramIds =
            await db.TeacherPrograms
                .AsNoTracking()
                .Where(tp =>
                    tp.UserId == userId
                )
                .Select(tp =>
                    tp.ProgramId
                )
                .ToListAsync();


        List<string> selectedCourseIds =
            await db.TeacherCourses
                .AsNoTracking()
                .Where(tc =>
                    tc.UserId == userId
                )
                .Select(tc =>
                    tc.CourseId
                )
                .ToListAsync();


        TeacherProfileSetupViewModel model =
            new()
            {
                SelectedProgramIds =
                    selectedProgramIds,

                SelectedCourseIds =
                    selectedCourseIds
            };


        await LoadTeacherSetupOptionsAsync(
            model
        );


        return View(model);
    }


    // ==================================================
    // LOAD TEACHER SETUP OPTIONS
    // ==================================================

    private async Task LoadTeacherSetupOptionsAsync(
        TeacherProfileSetupViewModel model)
    {
        model.Programs =
            await db.AcademicPrograms
                .AsNoTracking()
                .OrderBy(p =>
                    p.ProgramCode
                )
                .ToListAsync();


        model.Subjects =
            await db.ProgramCourses
                .AsNoTracking()
                .Include(pc =>
                    pc.Course
                )
                .Where(pc =>
                    pc.Course != null
                )
                .OrderBy(pc =>
                    pc.ProgramId
                )
                .ThenBy(pc =>
                    pc.Course!.CourseName
                )
                .Select(pc =>
                    new TeacherSubjectOptionViewModel
                    {
                        ProgramId =
                            pc.ProgramId,

                        CourseId =
                            pc.CourseId,

                        CourseName =
                            pc.Course!.CourseName,

                        CourseCode =
                            pc.Course.CourseCode
                    }
                )
                .ToListAsync();
    }


    // ==================================================
    // SUBJECTS BY PROGRAM
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Subjects(
        string programId)
    {
        var rows =
            await db.ProgramCourses
                .AsNoTracking()
                .Where(pc =>
                    pc.ProgramId ==
                    programId
                )
                .Include(pc =>
                    pc.Course
                )
                .OrderBy(pc =>
                    pc.Course!.CourseName
                )
                .Select(pc =>
                    new
                    {
                        id =
                            pc.CourseId,

                        name =
                            pc.Course!.CourseName,

                        code =
                            pc.Course.CourseCode
                    }
                )
                .ToListAsync();


        return Json(rows);
    }


    // ==================================================
    // SAVE TEACHER PROFILE
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        TeacherProfileSetupViewModel model)
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


        model.SelectedProgramIds =
            model.SelectedProgramIds
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x
                    )
                )
                .Distinct()
                .ToList();


        model.SelectedCourseIds =
            model.SelectedCourseIds
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x
                    )
                )
                .Distinct()
                .ToList();


        if (
            model.SelectedProgramIds.Count ==
            0
        )
        {
            ModelState.AddModelError(
                "",
                "Please select at least one program."
            );
        }


        if (
            model.SelectedCourseIds.Count ==
            0
        )
        {
            ModelState.AddModelError(
                "",
                "Please select at least one subject."
            );
        }


        if (!ModelState.IsValid)
        {
            await LoadTeacherSetupOptionsAsync(
                model
            );

            return View(model);
        }


        List<string> validProgramIds =
            await db.AcademicPrograms
                .AsNoTracking()
                .Where(p =>
                    model.SelectedProgramIds
                        .Contains(
                            p.ProgramId
                        )
                )
                .Select(p =>
                    p.ProgramId
                )
                .ToListAsync();


        if (
            validProgramIds.Count !=
            model.SelectedProgramIds.Count
        )
        {
            ModelState.AddModelError(
                "",
                "One or more selected programs are invalid."
            );


            await LoadTeacherSetupOptionsAsync(
                model
            );


            return View(model);
        }


        var validMappings =
            await db.ProgramCourses
                .AsNoTracking()
                .Where(pc =>
                    model.SelectedProgramIds
                        .Contains(
                            pc.ProgramId
                        ) &&
                    model.SelectedCourseIds
                        .Contains(
                            pc.CourseId
                        )
                )
                .Select(pc =>
                    new
                    {
                        pc.ProgramId,
                        pc.CourseId
                    }
                )
                .ToListAsync();


        List<string> validCourseIds =
            validMappings
                .Select(x =>
                    x.CourseId
                )
                .Distinct()
                .ToList();


        if (
            validCourseIds.Count !=
            model.SelectedCourseIds.Count
        )
        {
            ModelState.AddModelError(
                "",
                "One or more selected subjects do not belong to the selected programs."
            );


            await LoadTeacherSetupOptionsAsync(
                model
            );


            return View(model);
        }


        foreach (
            string programId
            in model.SelectedProgramIds
        )
        {
            bool hasSubject =
                validMappings.Any(x =>
                    x.ProgramId ==
                    programId
                );


            if (!hasSubject)
            {
                ModelState.AddModelError(
                    "",
                    "Please select at least one subject for every selected program."
                );


                await LoadTeacherSetupOptionsAsync(
                    model
                );


                return View(model);
            }
        }


        Teacher? teacher =
            await db.Teacher
                .FirstOrDefaultAsync(t =>
                    t.UserId ==
                    userId
                );


        if (teacher == null)
        {
            teacher =
                new Teacher
                {
                    TeacherId =
                        await FormattedIdGenerator
                            .NextAsync(
                                db.Teacher,
                                t =>
                                    t.TeacherId,
                                "TCH"
                            ),

                    UserId =
                        userId
                };


            db.Teacher.Add(
                teacher
            );
        }


        List<string> programCodes =
            await db.AcademicPrograms
                .AsNoTracking()
                .Where(p =>
                    model.SelectedProgramIds
                        .Contains(
                            p.ProgramId
                        )
                )
                .OrderBy(p =>
                    p.ProgramCode
                )
                .Select(p =>
                    p.ProgramCode
                )
                .ToListAsync();


        List<string> courseNames =
            await db.Courses
                .AsNoTracking()
                .Where(c =>
                    model.SelectedCourseIds
                        .Contains(
                            c.CourseId
                        )
                )
                .OrderBy(c =>
                    c.CourseName
                )
                .Select(c =>
                    c.CourseName
                )
                .ToListAsync();


        teacher.Program =
            string.Join(
                ", ",
                programCodes
            );


        teacher.Subject =
            string.Join(
                ", ",
                courseNames
            );


        await using var transaction =
            await db.Database
                .BeginTransactionAsync();


        try
        {
            List<TeacherProgram> existingPrograms =
                await db.TeacherPrograms
                    .Where(tp =>
                        tp.UserId ==
                        userId
                    )
                    .ToListAsync();


            List<TeacherProgram> programsToRemove =
                existingPrograms
                    .Where(tp =>
                        !model.SelectedProgramIds
                            .Contains(
                                tp.ProgramId
                            )
                    )
                    .ToList();


            db.TeacherPrograms
                .RemoveRange(
                    programsToRemove
                );


            HashSet<string> existingProgramIds =
                existingPrograms
                    .Select(tp =>
                        tp.ProgramId
                    )
                    .ToHashSet();


            foreach (
                string programId
                in model.SelectedProgramIds
            )
            {
                if (
                    !existingProgramIds.Contains(
                        programId
                    )
                )
                {
                    db.TeacherPrograms.Add(
                        new TeacherProgram
                        {
                            UserId =
                                userId,

                            ProgramId =
                                programId,

                            RegisteredDate =
                                DateTime.Now
                        }
                    );
                }
            }


            List<TeacherCourse> existingCourses =
                await db.TeacherCourses
                    .Where(tc =>
                        tc.UserId ==
                        userId
                    )
                    .ToListAsync();


            List<TeacherCourse> coursesToRemove =
                existingCourses
                    .Where(tc =>
                        !model.SelectedCourseIds
                            .Contains(
                                tc.CourseId
                            )
                    )
                    .ToList();


            db.TeacherCourses
                .RemoveRange(
                    coursesToRemove
                );


            await db.SaveChangesAsync();


            List<string> remainingCourseIds =
                await db.TeacherCourses
                    .Where(tc =>
                        tc.UserId ==
                        userId
                    )
                    .Select(tc =>
                        tc.CourseId
                    )
                    .ToListAsync();


            foreach (
                string courseId
                in model.SelectedCourseIds
            )
            {
                if (
                    !remainingCourseIds.Contains(
                        courseId
                    )
                )
                {
                    TeacherCourse teacherCourse =
                        new()
                        {
                            TeacherCourseId =
                                await FormattedIdGenerator
                                    .NextAsync(
                                        db.TeacherCourses,
                                        x =>
                                            x.TeacherCourseId,
                                        "TCR"
                                    ),

                            UserId =
                                userId,

                            CourseId =
                                courseId,

                            RegisteredDate =
                                DateTime.Now
                        };


                    db.TeacherCourses.Add(
                        teacherCourse
                    );


                    await db.SaveChangesAsync();
                }
            }


            await transaction
                .CommitAsync();
        }
        catch
        {
            await transaction
                .RollbackAsync();

            throw;
        }


        return RedirectToAction(
            "Index",
            "MyProfile"
        );
    }


    // ==================================================
    // TEACHER PROFILE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Profile()
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


        Teacher? teacher =
            await db.Teacher
                .FirstOrDefaultAsync(t =>
                    t.UserId ==
                    userId
                );


        if (teacher == null)
        {
            return RedirectToAction(
                nameof(Create)
            );
        }


        await TryLinkLegacyProfileAsync(
            userId
        );


        if (
            !await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId ==
                    userId
                )
        )
        {
            return RedirectToAction(
                nameof(Create)
            );
        }


        return View(teacher);
    }


    // ==================================================
    // LEGACY PROFILE LINK
    // ==================================================

    private async Task TryLinkLegacyProfileAsync(
        string userId)
    {
        if (
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId ==
                    userId
                )
        )
        {
            return;
        }


        Teacher? teacher =
            await db.Teacher
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.UserId ==
                    userId
                );


        if (
            teacher == null ||
            string.IsNullOrWhiteSpace(
                teacher.Program
            ) ||
            string.IsNullOrWhiteSpace(
                teacher.Subject
            )
        )
        {
            return;
        }


        AcademicProgram? program =
            await db.AcademicPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProgramCode
                        .ToLower() ==
                    teacher.Program
                        .ToLower()
                );


        if (program == null)
        {
            return;
        }


        ProgramCourse? mapping =
            await db.ProgramCourses
                .AsNoTracking()
                .Include(pc =>
                    pc.Course
                )
                .FirstOrDefaultAsync(pc =>
                    pc.ProgramId ==
                        program.ProgramId &&
                    pc.Course !=
                        null &&
                    pc.Course.CourseName
                        .ToLower() ==
                    teacher.Subject
                        .ToLower()
                );


        if (mapping == null)
        {
            return;
        }


        db.TeacherCourses.Add(
            new TeacherCourse
            {
                TeacherCourseId =
                    await FormattedIdGenerator
                        .NextAsync(
                            db.TeacherCourses,
                            x =>
                                x.TeacherCourseId,
                            "TCR"
                        ),

                UserId =
                    userId,

                CourseId =
                    mapping.CourseId,

                RegisteredDate =
                    DateTime.Now
            }
        );


        await db.SaveChangesAsync();
    }


    // ==================================================
    // GET DATE FOR DAY
    // ==================================================

    private static DateTime GetDateForDay(
        DateTime weekStart,
        DayOfWeek day)
    {
        int difference =
            (
                (int)day -
                (int)weekStart.DayOfWeek +
                7
            ) % 7;


        return weekStart
            .AddDays(
                difference
            )
            .Date;
    }


    // ==================================================
    // DAY ORDER
    // ==================================================

    private static int GetDayOrder(
        DayOfWeek day)
    {
        return day ==
            DayOfWeek.Sunday
                ? 7
                : (int)day;
    }


    // ==================================================
    // PENDING DECISION
    // ==================================================

    private static bool IsPendingDecision(
        string? value)
    {
        return string.Equals(
            value,
            "Pending",
            StringComparison.OrdinalIgnoreCase
        );
    }


    // ==================================================
    // CANCELLED STATUS
    // ==================================================

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
            .Contains(
                "cancel",
                StringComparison.OrdinalIgnoreCase
            );
    }
}