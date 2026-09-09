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
    private readonly IAvatarService avatarService;

    public StudentController(
        DB db,
        IWebHostEnvironment environment,
        IAvatarService avatarService)
    {
        this.db = db;
        this.environment = environment;
        this.avatarService = avatarService;
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
            currentUser == null
                ? "/image/default.png"
                : avatarService.ResolveAvatar(currentUser.UserId, currentUser.Avatar);


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

            // ==========================================
            // REGISTRATION TIME VALIDATION
            // ==========================================

            todayTimetable =
                todayTimetable
                    .Where(t =>
                    {
                        DateTime classStart =
                            DateTime.Today.Add(
                                t.StartTime
                            );

                        return studentClass.RegisteredDate <=
                            classStart;
                    })
                    .ToList();
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
    public async Task<IActionResult> Timetable(
        int? weekNumber = null)
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


        // ==================================================
        // STUDENT CLASS
        // ==================================================

        StudentClass? studentClass =
            await db.StudentClasses
                .AsNoTracking()
                .Include(sc =>
                    sc.ClassGroup
                )
                .Include(sc =>
                    sc.Program
                )
                .FirstOrDefaultAsync(sc =>
                    sc.UserId ==
                        studentId
                );


        TeacherTimetableViewModel model =
            new();


        if (studentClass == null)
        {
            model.TermName =
                "No Active Semester";

            return View(model);
        }


        ViewBag.ClassName =
            studentClass.ClassGroup?.ClassName
            ?? studentClass.ClassId;

        ViewBag.ProgramName =
            studentClass.Program?.ProgramName
            ?? studentClass.ProgramId;


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


        if (activeTerm == null)
        {
            model.TermName =
                "No Active Semester";

            ViewBag.TermName =
                model.TermName;

            return View(model);
        }


        model.TermName =
            activeTerm.Name;

        ViewBag.TermName =
            activeTerm.Name;


        // ==================================================
        // ACADEMIC WEEKS
        // Only weeks belonging to the Active semester.
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
        // CURRENT STUDENT CLASS TIMETABLE
        // Only rows from the Active semester.
        // ==================================================

        List<Timetable> entries =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId ==
                        studentClass.ClassId &&
                    t.TermId ==
                        activeTerm.TermId
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
                .Include(t =>
                    t.Teacher
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
                            entry.Course?.CourseName
                            ?? entry.CourseId,

                        ClassName =
                            entry.ClassGroup?.ClassName
                            ?? entry.ClassId,

                        RoomName =
                            entry.Classroom?.RoomName
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
            model.WeekStart.HasValue &&
            model.WeekEnd.HasValue
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
            // Cancelled / changed time / changed classroom.
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
            // Filter by the source timetable's class so the
            // replacement is shown to students in that class.
            // ==================================================

            List<ReplacementSession> replacements =
                await db.ReplacementSessions
                    .AsNoTracking()
                    .Where(r =>
                        r.SourceTimetable !=
                            null &&
                        r.SourceTimetable.ClassId ==
                            studentClass.ClassId &&
                        r.SourceTimetable.TermId ==
                            activeTerm.TermId &&
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


            Dictionary<string, string> roomNames =
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
            // NORMAL / CANCELLED / UPDATED LESSONS
            // ==================================================

            foreach (
                Timetable entry
                in entries
            )
            {
                DateTime occurrenceDate =
                    GetStudentTimetableDateForDay(
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


                // Student should not see lessons before the
                // date they joined this class.
                if (
                    occurrenceDate <
                        studentClass.RegisteredDate.Date
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
                            !IsStudentTimetablePendingDecision(
                                e.Decision
                            )
                        );


                bool cancelled =
                    exception != null &&
                    IsStudentTimetableCancelledValue(
                        exception.Decision
                    );


                TimeSpan actualStart =
                    exception?.OverrideStartTime
                    ?? entry.StartTime;


                TimeSpan actualEnd =
                    exception?.OverrideEndTime
                    ?? entry.EndTime;


                string roomName =
                    entry.Classroom?.RoomName
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
                            entry.Course?.CourseName
                            ?? entry.CourseId,

                        ClassName =
                            entry.ClassGroup?.ClassName
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
                    IsStudentTimetableCancelledValue(
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


                if (
                    replacement.ReplacementDate.Date <
                        studentClass.RegisteredDate.Date
                )
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
                            source.Course?.CourseName
                            ?? source.CourseId,

                        ClassName =
                            source.ClassGroup?.ClassName
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
        // RECENT TIMETABLE NOTIFICATIONS
        // - Last 7 days only on timetable page
        // - Latest state per lesson
        // - Maximum 3 shown in the recent box
        // ==================================================

        DateTime notificationCutOff =
            DateTime.Now.AddDays(-7);


        List<TimetableNotification> recentNotifications =
            await db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>
                    n.UserId ==
                        studentId &&
                    n.CreatedAt >=
                        notificationCutOff &&
                    n.TermId ==
                        activeTerm.TermId
                )
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .ToListAsync();


        List<TimetableNotification> latestRecentNotifications =
            recentNotifications
                .GroupBy(n =>
                    !string.IsNullOrWhiteSpace(
                        n.TimetableId
                    ) &&
                    n.OccurrenceDate.HasValue

                        ? $"{n.TimetableId}|{n.OccurrenceDate.Value:yyyyMMdd}"
                        : n.NotificationId
                )
                .Select(g =>
                    g.OrderByDescending(n =>
                        n.CreatedAt
                    )
                    .First()
                )
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .ToList();


        // If an old cancellation notification is still in history
        // but the cancellation was restored, hide that stale notice
        // from the Recent Timetable Changes box.
        List<TimetableNotification> cancellationNotifications =
            latestRecentNotifications
                .Where(n =>
                    (
                        string.Equals(
                            n.Type,
                            "LessonCancelled",
                            StringComparison.OrdinalIgnoreCase
                        ) ||
                        string.Equals(
                            n.Type,
                            "ClassCancelled",
                            StringComparison.OrdinalIgnoreCase
                        ) ||
                        string.Equals(
                            n.Type,
                            "Cancelled",
                            StringComparison.OrdinalIgnoreCase
                        )
                    ) &&
                    !string.IsNullOrWhiteSpace(
                        n.TimetableId
                    ) &&
                    n.OccurrenceDate.HasValue
                )
                .ToList();


        if (cancellationNotifications.Count > 0)
        {
            List<string> cancelledTimetableIds =
                cancellationNotifications
                    .Select(n =>
                        n.TimetableId!
                    )
                    .Distinct()
                    .ToList();


            DateTime earliestCancellationDate =
                cancellationNotifications
                    .Min(n =>
                        n.OccurrenceDate!.Value.Date
                    );


            List<TimetableException> activeCancellationExceptions =
                await db.TimetableExceptions
                    .AsNoTracking()
                    .Where(e =>
                        cancelledTimetableIds.Contains(
                            e.TimetableId
                        ) &&
                        e.OccurrenceDate.Date >=
                            earliestCancellationDate
                    )
                    .ToListAsync();


            activeCancellationExceptions =
                activeCancellationExceptions
                    .Where(e =>
                        IsStudentTimetableCancelledValue(
                            e.Decision
                        )
                    )
                    .ToList();


            HashSet<string> activeCancellationKeys =
                activeCancellationExceptions
                    .Select(e =>
                        $"{e.TimetableId}|{e.OccurrenceDate.Date:yyyyMMdd}"
                    )
                    .ToHashSet();


            latestRecentNotifications =
                latestRecentNotifications
                    .Where(n =>
                    {
                        bool isCancellation =
                            string.Equals(
                                n.Type,
                                "LessonCancelled",
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                n.Type,
                                "ClassCancelled",
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                n.Type,
                                "Cancelled",
                                StringComparison.OrdinalIgnoreCase
                            );


                        if (
                            !isCancellation ||
                            string.IsNullOrWhiteSpace(
                                n.TimetableId
                            ) ||
                            !n.OccurrenceDate.HasValue
                        )
                        {
                            return true;
                        }


                        string key =
                            $"{n.TimetableId}|{n.OccurrenceDate.Value.Date:yyyyMMdd}";


                        return activeCancellationKeys
                            .Contains(
                                key
                            );
                    })
                    .ToList();
        }


        ViewBag.RecentNotificationCount =
            latestRecentNotifications
                .Count;


        model.Notifications =
            latestRecentNotifications
                .Take(3)
                .ToList();


        // ==================================================
        // SORT TIMETABLE
        // ==================================================

        model.Items =
            model.Items
                .OrderBy(i =>
                    GetStudentTimetableDayOrder(
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
    // VIEW ALL TIMETABLE CHANGES
    // ==================================================

    [HttpGet]
    [Authorize(Roles = "Student")]
    [RequireStudentProfile]
    public async Task<IActionResult> TimetableChanges(
        int page = 1)
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

        const int pageSize = 10;

        IQueryable<TimetableNotification> query =
            db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>
                    n.UserId == studentId
                );

        int totalCount =
            await query.CountAsync();

        int totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)pageSize
            );

        if (totalPages < 1)
        {
            totalPages = 1;
        }

        page =
            Math.Clamp(
                page,
                1,
                totalPages
            );

        List<TimetableNotification> notifications =
            await query
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .Skip(
                    (page - 1) *
                    pageSize
                )
                .Take(
                    pageSize
                )
                .ToListAsync();

        ViewBag.CurrentPage =
            page;

        ViewBag.TotalPages =
            totalPages;

        ViewBag.TotalCount =
            totalCount;

        ViewBag.PageSize =
            pageSize;

        return View(
            notifications
        );
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

        // ==================================================
        // BUILD ALL LESSON OCCURRENCES FROM THE TIMETABLE
        // ==================================================
        //
        // IMPORTANT:
        // Attendance is now based on the lesson that SHOULD
        // have happened, not only on AttendanceSession.
        //
        // Therefore:
        // - teacher generated code -> session may exist
        // - teacher forgot code     -> session may NOT exist
        //
        // Once the lesson ends, a student with no Present
        // record and no approved leave is counted Absent.
        // ==================================================

        List<StudentAttendanceOccurrence> allOccurrences =
            await GetStudentAttendanceOccurrencesAsync(
                studentClass,
                null,
                studentClass.RegisteredDate.Date,
                today
            );

        List<string> occurrenceTimetableIds =
            allOccurrences
                .Select(o => o.TimetableId)
                .Distinct()
                .ToList();

        DateTime firstOccurrenceDate =
            allOccurrences.Count == 0
                ? today
                : allOccurrences.Min(o => o.Date);

        List<AttendanceSession> sessions =
            occurrenceTimetableIds.Count == 0
                ? new List<AttendanceSession>()
                : await db.AttendanceSessions
                    .AsNoTracking()
                    .Where(s =>
                        occurrenceTimetableIds.Contains(
                            s.TimetableId
                        ) &&
                        s.SessionDate >=
                            firstOccurrenceDate &&
                        s.SessionDate <=
                            today
                    )
                    .ToListAsync();

        Dictionary<string, AttendanceSession> sessionMap =
            sessions
                .GroupBy(s =>
                    GetAttendanceOccurrenceKey(
                        s.TimetableId,
                        s.SessionDate
                    )
                )
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s =>
                            s.GeneratedAt
                        )
                        .First()
                );

        List<string> attendanceSessionIds =
            sessions
                .Select(s =>
                    s.AttendanceSessionId
                )
                .ToList();

        List<AttendanceRecord> records =
            attendanceSessionIds.Count == 0
                ? new List<AttendanceRecord>()
                : await db.AttendanceRecords
                    .AsNoTracking()
                    .Where(r =>
                        attendanceSessionIds.Contains(
                            r.AttendanceSessionId
                        ) &&
                        r.StudentClassId ==
                            studentClass.StudentClassId
                    )
                    .ToListAsync();

        Dictionary<string, AttendanceRecord> recordMap =
            records
                .GroupBy(r =>
                    r.AttendanceSessionId
                )
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(r =>
                            r.SubmittedAt
                        )
                        .First()
                );

        List<LeaveApplication> approvedLeaves =
            await db.LeaveApplications
                .AsNoTracking()
                .Where(l =>
                    l.StudentId ==
                        studentId &&
                    l.Status ==
                        "Approved" &&
                    l.EndDate.Date >=
                        firstOccurrenceDate &&
                    l.StartDate.Date <=
                        today
                )
                .ToListAsync();


        // ==================================================
        // TODAY'S ATTENDANCE
        // ==================================================

        List<StudentAttendanceOccurrence> todayOccurrences =
            allOccurrences
                .Where(o =>
                    o.Date ==
                    today
                )
                .OrderBy(o =>
                    o.StartTime
                )
                .ToList();

        foreach (
            StudentAttendanceOccurrence occurrence
            in todayOccurrences
        )
        {
            string key =
                GetAttendanceOccurrenceKey(
                    occurrence.TimetableId,
                    occurrence.Date
                );

            sessionMap.TryGetValue(
                key,
                out AttendanceSession? session
            );

            AttendanceRecord? record =
                null;

            if (
                session != null
            )
            {
                recordMap.TryGetValue(
                    session.AttendanceSessionId,
                    out record
                );
            }

            LeaveApplication? approvedLeave =
                approvedLeaves
                    .FirstOrDefault(l =>
                        occurrence.Date >=
                            l.StartDate.Date &&
                        occurrence.Date <=
                            l.EndDate.Date
                    );

            string status =
                ResolveStudentAttendanceStatus(
                    occurrence,
                    session,
                    record,
                    approvedLeave,
                    now
                );

            model.TodayAttendance.Add(
                new TodayAttendanceItemViewModel
                {
                    CourseId =
                        occurrence.CourseId,

                    CourseCode =
                        occurrence.CourseCode,

                    CourseName =
                        occurrence.CourseName,

                    StartTime =
                        occurrence.StartTime,

                    EndTime =
                        occurrence.EndTime,

                    Status =
                        status
                }
            );
        }


        // ==================================================
        // OVERALL ATTENDANCE
        // ==================================================

        List<string> activeCourseIds =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId ==
                        studentClass.ClassId &&
                    t.Term != null &&
                    t.Term.Status ==
                        "Active"
                )
                .Select(t =>
                    t.CourseId
                )
                .Distinct()
                .ToListAsync();

        List<Course> activeCourses =
            await db.Courses
                .AsNoTracking()
                .Where(c =>
                    activeCourseIds.Contains(
                        c.CourseId
                    )
                )
                .OrderBy(c =>
                    c.CourseName
                )
                .ToListAsync();

        foreach (
            Course course
            in activeCourses
        )
        {
            List<StudentAttendanceOccurrence> courseOccurrences =
                allOccurrences
                    .Where(o =>
                        o.CourseId ==
                            course.CourseId &&
                        now >=
                            o.Date.Add(
                                o.EndTime
                            )
                    )
                    .OrderBy(o =>
                        o.Date
                    )
                    .ThenBy(o =>
                        o.StartTime
                    )
                    .ToList();

            int present =
                0;

            int absent =
                0;

            int leave =
                0;

            foreach (
                StudentAttendanceOccurrence occurrence
                in courseOccurrences
            )
            {
                string key =
                    GetAttendanceOccurrenceKey(
                        occurrence.TimetableId,
                        occurrence.Date
                    );

                sessionMap.TryGetValue(
                    key,
                    out AttendanceSession? session
                );

                AttendanceRecord? record =
                    null;

                if (
                    session != null
                )
                {
                    recordMap.TryGetValue(
                        session.AttendanceSessionId,
                        out record
                    );
                }

                LeaveApplication? approvedLeave =
                    approvedLeaves
                        .FirstOrDefault(l =>
                            occurrence.Date >=
                                l.StartDate.Date &&
                            occurrence.Date <=
                                l.EndDate.Date
                        );

                string status =
                    ResolveStudentAttendanceStatus(
                        occurrence,
                        session,
                        record,
                        approvedLeave,
                        now
                    );

                if (
                    status ==
                    "Present"
                )
                {
                    present++;
                }
                else if (
                    status ==
                    "Leave"
                )
                {
                    leave++;
                }
                else
                {
                    // The occurrence has already ended.
                    // Anything that is not Present or Leave
                    // is counted as Absent.
                    absent++;
                }
            }

            // ==================================================
            // ATTENDANCE PERCENTAGE
            // ==================================================
            //
            // Formula:
            // (Present + 4) /
            // (Present + Absent + 4) × 100
            //
            // One first absence:
            // (0 + 4) / (0 + 1 + 4) = 80%
            //
            // Approved Leave is excluded.
            // ==================================================

            const int AttendanceStartingBuffer =
                4;

            int countedClasses =
                present +
                absent;

            double percentage =
                countedClasses == 0
                    ? 100
                    : Math.Round(
                        (
                            present +
                            AttendanceStartingBuffer
                        ) *
                        100.0 /
                        (
                            present +
                            absent +
                            AttendanceStartingBuffer
                        ),
                        1
                    );

            model.Subjects.Add(
                new SubjectAttendanceViewModel
                {
                    CourseId =
                        course.CourseId,

                    CourseCode =
                        course.CourseCode
                        ?? "",

                    CourseName =
                        course.CourseName,

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


        // ==============================
        // BARRED WARNING
        // ==============================

        List<BarredStudent> barredRecords =
            await db.BarredStudents
                .AsNoTracking()
                .Where(b =>
                    b.UserId == studentId
                )
                .Include(b =>
                    b.Course
                )
                .ToListAsync();

        List<string> barredWarnings =
            new();

        foreach (
            BarredStudent barred
            in barredRecords
        )
        {
            SubjectAttendanceViewModel? subject =
                model.Subjects
                    .FirstOrDefault(s =>
                        s.CourseId ==
                        barred.CourseId
                    );

            if (
                subject ==
                null
            )
            {
                continue;
            }

            double currentPercentage =
                Math.Round(
                    subject.Percentage,
                    1
                );

            if (
                currentPercentage >
                80
            )
            {
                continue;
            }

            string courseName =
                barred.Course?.CourseName
                ?? subject.CourseName;

            barredWarnings.Add(
                $"You are currently barred from {courseName}. "
            );
        }

        ViewBag.BarredWarnings =
            barredWarnings;

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
            code.Length !=
                6 ||
            !code.All(
                char.IsDigit
            )
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

        // ==============================
        // REGISTRATION TIME VALIDATION
        // ==============================

        DateTime classStart =
            session.SessionDate.Date
                .Add(
                    session.Timetable!.StartTime
                );

        if (
            studentClass.RegisteredDate >
            classStart
        )
        {
            TempData["AttendanceError"] =
                "This class started before you completed your program and class registration.";

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

        bool courseInActiveSemester =
            await db.Timetables
                .AsNoTracking()
                .AnyAsync(t =>
                    t.ClassId ==
                        studentClass.ClassId &&
                    t.CourseId ==
                        courseId &&
                    t.Term != null &&
                    t.Term.Status ==
                        "Active"
                );

        if (
            !courseInActiveSemester
        )
        {
            return NotFound();
        }

        DateTime now =
            DateTime.Now;

        DateTime today =
            DateTime.Today;

        List<StudentAttendanceOccurrence> occurrences =
            await GetStudentAttendanceOccurrencesAsync(
                studentClass,
                courseId,
                studentClass.RegisteredDate.Date,
                today
            );

        List<StudentAttendanceOccurrence> endedOccurrences =
            occurrences
                .Where(o =>
                    now >=
                        o.Date.Add(
                            o.EndTime
                        )
                )
                .OrderBy(o =>
                    o.Date
                )
                .ThenBy(o =>
                    o.StartTime
                )
                .ToList();

        List<string> timetableIds =
            endedOccurrences
                .Select(o =>
                    o.TimetableId
                )
                .Distinct()
                .ToList();

        DateTime firstOccurrenceDate =
            endedOccurrences.Count == 0
                ? today
                : endedOccurrences.Min(o =>
                    o.Date
                );

        List<AttendanceSession> sessions =
            timetableIds.Count == 0
                ? new List<AttendanceSession>()
                : await db.AttendanceSessions
                    .AsNoTracking()
                    .Where(s =>
                        timetableIds.Contains(
                            s.TimetableId
                        ) &&
                        s.SessionDate >=
                            firstOccurrenceDate &&
                        s.SessionDate <=
                            today
                    )
                    .ToListAsync();

        Dictionary<string, AttendanceSession> sessionMap =
            sessions
                .GroupBy(s =>
                    GetAttendanceOccurrenceKey(
                        s.TimetableId,
                        s.SessionDate
                    )
                )
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s =>
                            s.GeneratedAt
                        )
                        .First()
                );

        List<string> attendanceSessionIds =
            sessions
                .Select(s =>
                    s.AttendanceSessionId
                )
                .ToList();

        List<AttendanceRecord> records =
            attendanceSessionIds.Count == 0
                ? new List<AttendanceRecord>()
                : await db.AttendanceRecords
                    .AsNoTracking()
                    .Where(r =>
                        attendanceSessionIds.Contains(
                            r.AttendanceSessionId
                        ) &&
                        r.StudentClassId ==
                            studentClass.StudentClassId
                    )
                    .ToListAsync();

        Dictionary<string, AttendanceRecord> recordMap =
            records
                .GroupBy(r =>
                    r.AttendanceSessionId
                )
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(r =>
                            r.SubmittedAt
                        )
                        .First()
                );

        List<LeaveApplication> approvedLeaves =
            await db.LeaveApplications
                .AsNoTracking()
                .Where(l =>
                    l.StudentId ==
                        studentId &&
                    l.Status ==
                        "Approved" &&
                    l.EndDate.Date >=
                        firstOccurrenceDate &&
                    l.StartDate.Date <=
                        today
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
            StudentAttendanceOccurrence occurrence
            in endedOccurrences
        )
        {
            string key =
                GetAttendanceOccurrenceKey(
                    occurrence.TimetableId,
                    occurrence.Date
                );

            sessionMap.TryGetValue(
                key,
                out AttendanceSession? session
            );

            AttendanceRecord? record =
                null;

            if (
                session != null
            )
            {
                recordMap.TryGetValue(
                    session.AttendanceSessionId,
                    out record
                );
            }

            LeaveApplication? approvedLeave =
                approvedLeaves
                    .FirstOrDefault(l =>
                        occurrence.Date >=
                            l.StartDate.Date &&
                        occurrence.Date <=
                            l.EndDate.Date
                    );

            string status =
                ResolveStudentAttendanceStatus(
                    occurrence,
                    session,
                    record,
                    approvedLeave,
                    now
                );

            string? description =
                null;

            if (
                status ==
                "Leave" &&
                approvedLeave !=
                    null
            )
            {
                description =
                    string.IsNullOrWhiteSpace(
                        approvedLeave.Reason
                    )
                        ? approvedLeave.LeaveType
                        : $"{approvedLeave.LeaveType}: {approvedLeave.Reason}";
            }
            else if (
                status ==
                "Present"
            )
            {
                description =
                    record?.Description;
            }
            else if (
                status ==
                "Absent"
            )
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        record?.Description
                    )
                )
                {
                    description =
                        record!.Description;
                }
                else if (
                    session ==
                        null ||
                    !session.IsActive
                )
                {
                    description =
                        "Attendance code was not generated.";
                }
            }

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

            model.History.Add(
                new AttendanceHistoryItemViewModel
                {
                    Date =
                        occurrence.Date,

                    StartTime =
                        occurrence.StartTime,

                    EndTime =
                        occurrence.EndTime,

                    Status =
                        status,

                    Description =
                        description
                }
            );
        }

        // ==================================================
        // ATTENDANCE PERCENTAGE
        // ==================================================

        const int AttendanceStartingBuffer =
            4;

        int countedClasses =
            model.PresentCount +
            model.AbsentCount;

        model.Percentage =
            countedClasses == 0
                ? 100
                : Math.Round(
                    (
                        model.PresentCount +
                        AttendanceStartingBuffer
                    ) *
                    100.0 /
                    (
                        model.PresentCount +
                        model.AbsentCount +
                        AttendanceStartingBuffer
                    ),
                    1
                );

        // ==================================================
        // DONUT CHART
        // ==================================================

        int total =
            model.PresentCount +
            model.AbsentCount +
            model.LeaveCount;

        if (
            total >
            0
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


    // ==================================================
    // STUDENT ATTENDANCE OCCURRENCES
    // ==================================================
    //
    // Builds lessons from Timetable itself.
    // This is what allows a lesson with NO AttendanceSession
    // to still become Absent after its end time.
    // ==================================================

    private async Task<List<StudentAttendanceOccurrence>>
        GetStudentAttendanceOccurrencesAsync(
            StudentClass studentClass,
            string? courseId,
            DateTime fromDate,
            DateTime toDate)
    {
        fromDate =
            fromDate.Date;

        toDate =
            toDate.Date;

        IQueryable<Timetable> timetableQuery =
            db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId ==
                        studentClass.ClassId &&
                    t.Term !=
                        null &&
                    t.Term.Status ==
                        "Active"
                );

        if (
            !string.IsNullOrWhiteSpace(
                courseId
            )
        )
        {
            timetableQuery =
                timetableQuery
                    .Where(t =>
                        t.CourseId ==
                            courseId
                    );
        }

        List<Timetable> timetables =
            await timetableQuery
                .Include(t =>
                    t.Term
                )
                .Include(t =>
                    t.Course
                )
                .ToListAsync();

        if (
            timetables.Count ==
            0
        )
        {
            return new List<StudentAttendanceOccurrence>();
        }

        List<string> timetableIds =
            timetables
                .Select(t =>
                    t.TimetableId
                )
                .ToList();

        List<TimetableException> exceptions =
            await db.TimetableExceptions
                .AsNoTracking()
                .Where(e =>
                    timetableIds.Contains(
                        e.TimetableId
                    ) &&
                    e.OccurrenceDate.Date >=
                        fromDate &&
                    e.OccurrenceDate.Date <=
                        toDate
                )
                .ToListAsync();

        List<ReplacementSession> replacements =
            await db.ReplacementSessions
                .AsNoTracking()
                .Where(r =>
                    timetableIds.Contains(
                        r.SourceTimetableId
                    ) &&
                    r.ReplacementDate.Date >=
                        fromDate &&
                    r.ReplacementDate.Date <=
                        toDate &&
                    r.Status ==
                        "Scheduled"
                )
                .ToListAsync();

        List<StudentAttendanceOccurrence> result =
            new();

        foreach (
            Timetable timetable
            in timetables
        )
        {
            if (
                timetable.Term ==
                null
            )
            {
                continue;
            }

            DateTime rangeStart =
                fromDate >
                timetable.Term.StartDate.Date
                    ? fromDate
                    : timetable.Term.StartDate.Date;

            DateTime rangeEnd =
                toDate <
                timetable.Term.EndDate.Date
                    ? toDate
                    : timetable.Term.EndDate.Date;

            if (
                rangeStart >
                rangeEnd
            )
            {
                continue;
            }

            int dayOffset =
                (
                    (int)timetable.DayOfWeek -
                    (int)rangeStart.DayOfWeek +
                    7
                ) %
                7;

            DateTime occurrenceDate =
                rangeStart.AddDays(
                    dayOffset
                );

            while (
                occurrenceDate <=
                rangeEnd
            )
            {
                TimetableException? exception =
                    exceptions
                        .FirstOrDefault(e =>
                            e.TimetableId ==
                                timetable.TimetableId &&
                            e.OccurrenceDate.Date ==
                                occurrenceDate &&
                            !string.Equals(
                                e.Decision,
                                "Pending",
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                bool cancelled =
                    exception !=
                        null &&
                    IsAttendanceCancelledValue(
                        exception.Decision
                    );

                bool movedToReplacement =
                    replacements
                        .Any(r =>
                            r.SourceTimetableId ==
                                timetable.TimetableId &&
                            r.SourceOccurrenceDate.HasValue &&
                            r.SourceOccurrenceDate.Value.Date ==
                                occurrenceDate
                        );

                if (
                    !cancelled &&
                    !movedToReplacement
                )
                {
                    TimeSpan startTime =
                        exception
                            ?.OverrideStartTime
                        ?? timetable.StartTime;

                    TimeSpan endTime =
                        exception
                            ?.OverrideEndTime
                        ?? timetable.EndTime;

                    DateTime classStart =
                        occurrenceDate.Add(
                            startTime
                        );

                    if (
                        studentClass.RegisteredDate <=
                        classStart
                    )
                    {
                        result.Add(
                            new StudentAttendanceOccurrence
                            {
                                TimetableId =
                                    timetable.TimetableId,

                                CourseId =
                                    timetable.CourseId,

                                CourseCode =
                                    timetable.Course?.CourseCode
                                    ?? "",

                                CourseName =
                                    timetable.Course?.CourseName
                                    ?? timetable.CourseId,

                                Date =
                                    occurrenceDate,

                                StartTime =
                                    startTime,

                                EndTime =
                                    endTime,

                                IsReplacement =
                                    false
                            }
                        );
                    }
                }

                occurrenceDate =
                    occurrenceDate.AddDays(
                        7
                    );
            }
        }

        foreach (
            ReplacementSession replacement
            in replacements
        )
        {
            Timetable? source =
                timetables
                    .FirstOrDefault(t =>
                        t.TimetableId ==
                            replacement.SourceTimetableId
                    );

            if (
                source ==
                null
            )
            {
                continue;
            }

            DateTime replacementDate =
                replacement.ReplacementDate.Date;

            DateTime replacementStart =
                replacementDate.Add(
                    replacement.StartTime
                );

            if (
                studentClass.RegisteredDate >
                replacementStart
            )
            {
                continue;
            }

            result.Add(
                new StudentAttendanceOccurrence
                {
                    TimetableId =
                        source.TimetableId,

                    CourseId =
                        source.CourseId,

                    CourseCode =
                        source.Course?.CourseCode
                        ?? "",

                    CourseName =
                        source.Course?.CourseName
                        ?? source.CourseId,

                    Date =
                        replacementDate,

                    StartTime =
                        replacement.StartTime,

                    EndTime =
                        replacement.EndTime,

                    IsReplacement =
                        true
                }
            );
        }

        return result
            .GroupBy(o =>
                $"{o.TimetableId}|" +
                $"{o.Date:yyyyMMdd}|" +
                $"{o.StartTime}"
            )
            .Select(g =>
                g.First()
            )
            .OrderBy(o =>
                o.Date
            )
            .ThenBy(o =>
                o.StartTime
            )
            .ToList();
    }


    private static string ResolveStudentAttendanceStatus(
        StudentAttendanceOccurrence occurrence,
        AttendanceSession? session,
        AttendanceRecord? record,
        LeaveApplication? approvedLeave,
        DateTime now)
    {
        if (
            approvedLeave !=
            null
        )
        {
            return "Leave";
        }

        if (
            record !=
                null &&
            !record.IsRemoved &&
            !string.Equals(
                record.Status,
                "Absent",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return "Present";
        }

        if (
            record !=
                null &&
            (
                record.IsRemoved ||
                string.Equals(
                    record.Status,
                    "Absent",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            return "Absent";
        }

        DateTime classEnd =
            occurrence.Date.Add(
                occurrence.EndTime
            );

        if (
            now >=
            classEnd
        )
        {
            return "Absent";
        }

        if (
            session !=
            null
        )
        {
            return "Not Marked";
        }

        return "Not Open";
    }


    private static string GetAttendanceOccurrenceKey(
        string timetableId,
        DateTime date)
    {
        return $"{timetableId}|{date.Date:yyyyMMdd}";
    }


    private static bool IsAttendanceCancelledValue(
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


    private class StudentAttendanceOccurrence
    {
        public string TimetableId { get; set; } =
            string.Empty;

        public string CourseId { get; set; } =
            string.Empty;

        public string CourseCode { get; set; } =
            string.Empty;

        public string CourseName { get; set; } =
            string.Empty;

        public DateTime Date { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsReplacement { get; set; }
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


    // ==================================================
    // STUDENT TIMETABLE - GET DATE FOR DAY
    // ==================================================

    private static DateTime GetStudentTimetableDateForDay(
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
    // STUDENT TIMETABLE - DAY ORDER
    // ==================================================

    private static int GetStudentTimetableDayOrder(
        DayOfWeek day)
    {
        return day ==
            DayOfWeek.Sunday
                ? 7
                : (int)day;
    }


    // ==================================================
    // STUDENT TIMETABLE - PENDING DECISION
    // ==================================================

    private static bool IsStudentTimetablePendingDecision(
        string? value)
    {
        return string.Equals(
            value,
            "Pending",
            StringComparison.OrdinalIgnoreCase
        );
    }


    // ==================================================
    // STUDENT TIMETABLE - CANCELLED STATUS
    // ==================================================

    private static bool IsStudentTimetableCancelledValue(
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