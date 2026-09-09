using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;
using WebAndMobile.Filters;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Teacher")]
[RequireTeacherProfile]
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


        bool hasTeacherProgram =
            await db.TeacherPrograms
                .AnyAsync(tp =>
                    tp.UserId == userId
                );


        bool hasTeacherCourse =
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == userId
                );


        if (
            !hasTeacherProgram ||
            !hasTeacherCourse
        )
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
                    t.Term != null &&
                    t.Term.Status ==
                        "Active"
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


        bool hasTeacherProgram =
            await db.TeacherPrograms
                .AnyAsync(tp =>
                    tp.UserId == userId
                );


        bool hasTeacherCourse =
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == userId
                );


        if (
            !hasTeacherProgram ||
            !hasTeacherCourse
        )
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
        // Only timetable rows that belong to the current
        // Active semester are shown.
        // Old / legacy TermId = NULL rows are NOT included.
        // ==================================================

        List<Timetable> entries =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.TeacherId ==
                        userId &&
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
                        r.SourceTimetable !=
                            null &&
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
        // RECENT TIMETABLE NOTIFICATIONS
        // Timetable page:
        // - only notifications from the last 7 days
        // - only show latest 3
        // - old records stay permanently in database
        // ==================================================

        DateTime notificationCutOff =
            DateTime.Now.AddDays(-7);


        List<TimetableNotification> recentNotifications =
            await db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>
                    n.UserId ==
                        userId &&
                    n.CreatedAt >=
                        notificationCutOff &&
                    n.TermId ==
                        activeTerm.TermId
                )
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .ToListAsync();


        // Same timetable + same lesson date = same lesson change.
        // Only the newest state is shown in Recent Timetable Changes.
        // Example:
        // LessonCancelled -> LessonRestored
        // Recent shows only LessonRestored.
        // View All still keeps both history records.
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
        // but the cancellation exception has already been removed/restored,
        // do not show that stale cancellation in the Recent box.
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


            // Use the SAME cancelled-status rule as the timetable grid.
            // This supports values such as:
            // Cancelled, Canceled, LessonCancelled, Class Cancelled, etc.
            activeCancellationExceptions =
                activeCancellationExceptions
                    .Where(e =>
                        IsCancelledValue(
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
    // VIEW ALL TIMETABLE CHANGES
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> TimetableChanges(
        int page = 1)
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


        const int pageSize =
            10;


        IQueryable<TimetableNotification> query =
            db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>
                    n.UserId ==
                        userId
                );


        int totalCount =
            await query
                .CountAsync();


        int totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)pageSize
            );


        if (totalPages < 1)
        {
            totalPages =
                1;
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


    // ==================================================
    // ATTENDANCE CODE PAGE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> AttendanceCode(
        DateTime? date = null)
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


        // ==================================================
        // TEACHER PROFILE CHECK
        // ==================================================

        bool hasTeacherProgram =
            await db.TeacherPrograms
                .AnyAsync(tp =>
                    tp.UserId == userId
                );


        bool hasTeacherCourse =
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == userId
                );


        if (
            !hasTeacherProgram ||
            !hasTeacherCourse
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
                    today,

                SelectedDate =
                    today
            };


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
            return View(model);
        }


        // ==================================================
        // DATE RANGE
        //
        // Semester start -> today
        //
        // Future classes are not added.
        // ==================================================

        DateTime rangeStart =
            activeTerm.StartDate.Date;


        DateTime rangeEnd =
            today <=
            activeTerm.EndDate.Date

                ? today

                : activeTerm.EndDate.Date;


        if (
            rangeEnd <
            rangeStart
        )
        {
            return View(model);
        }


        // ==================================================
        // ALL TEACHER TIMETABLES
        // ==================================================

        List<Timetable> timetables =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.TeacherId ==
                        userId &&

                    t.TermId ==
                        activeTerm.TermId
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


        List<string> timetableIds =
            timetables
                .Select(t =>
                    t.TimetableId
                )
                .Distinct()
                .ToList();


        // ==================================================
        // ALL TIMETABLE EXCEPTIONS
        // ==================================================

        List<TimetableException> exceptions =
            timetableIds.Count == 0

                ? new List<TimetableException>()

                : await db.TimetableExceptions.AsNoTracking().Where(e =>timetableIds.Contains(e.TimetableId) &&e.OccurrenceDate.Date >=rangeStart && e.OccurrenceDate.Date <=rangeEnd).ToListAsync();


        // ==================================================
        // ALL REPLACEMENT CLASSES
        // ==================================================

        List<ReplacementSession> replacements =
            await db.ReplacementSessions
                .AsNoTracking()
                .Where(r =>
                    r.TeacherId ==
                        userId &&

                    r.SourceTimetable !=
                        null &&

                    r.SourceTimetable.TermId ==
                        activeTerm.TermId &&

                    r.ReplacementDate.Date >=
                        rangeStart &&

                    r.ReplacementDate.Date <=
                        rangeEnd
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
                .Include(r =>
                    r.SourceTimetable
                )
                .ThenInclude(t =>
                    t!.Classroom
                )
                .ToListAsync();


        // ==================================================
        // ONLY SCHEDULED REPLACEMENTS
        // ==================================================

        replacements =
            replacements
                .Where(r =>
                    string.Equals(
                        r.Status,
                        "Scheduled",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .ToList();


        // ==================================================
        // SPECIAL CLASSROOM IDS
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
            roomIds.Count == 0

                ? new Dictionary<string, string>()

                : await db.Classrooms
                    .AsNoTracking()
                    .Where(r =>
                        roomIds.Contains(
                            r.ClassroomId
                        )
                    )
                    .ToDictionaryAsync(
                        r =>
                            r.ClassroomId,

                        r =>
                            r.RoomName
                    );


        // ==================================================
        // BUILD ALL CLASS OCCURRENCES
        // ==================================================

        List<TeacherAttendanceCodeItemViewModel>
            allClasses =
                new();


        // ==================================================
        // NORMAL CLASSES
        // ==================================================

        foreach (
            Timetable timetable
            in timetables
        )
        {
            // Find first occurrence of this weekday
            // starting from semester StartDate.

            int difference =
                (
                    (int)timetable.DayOfWeek -
                    (int)rangeStart.DayOfWeek +
                    7
                )
                %
                7;


            DateTime occurrenceDate =
                rangeStart.AddDays(
                    difference
                );


            while (
                occurrenceDate <=
                rangeEnd
            )
            {
                // ==============================================
                // EXCEPTION FOR THIS EXACT OCCURRENCE
                // ==============================================

                TimetableException? exception =
                    exceptions
                        .FirstOrDefault(e =>
                            e.TimetableId ==
                                timetable.TimetableId &&

                            e.OccurrenceDate.Date ==
                                occurrenceDate.Date &&

                            !IsPendingDecision(
                                e.Decision
                            )
                        );


                // ==============================================
                // CANCELLED CLASS
                // ==============================================

                bool cancelled =
                    exception != null &&
                    IsCancelledValue(
                        exception.Decision
                    );


                if (cancelled)
                {
                    occurrenceDate =
                        occurrenceDate
                            .AddDays(7);

                    continue;
                }


                // ==============================================
                // CHECK WHETHER ORIGINAL CLASS WAS MOVED
                // TO A REPLACEMENT SESSION
                //
                // If SourceOccurrenceDate is available,
                // do not show both the old and replacement class.
                // ==============================================

                bool movedToReplacement =
                    replacements
                        .Any(r =>
                            r.SourceTimetableId ==
                                timetable.TimetableId &&

                            r.SourceOccurrenceDate
                                .HasValue &&

                            r.SourceOccurrenceDate
                                .Value
                                .Date ==
                                occurrenceDate.Date
                        );


                if (movedToReplacement)
                {
                    occurrenceDate =
                        occurrenceDate
                            .AddDays(7);

                    continue;
                }


                // ==============================================
                // ACTUAL TIME
                // ==============================================

                TimeSpan startTime =
                    exception
                        ?.OverrideStartTime
                    ??
                    timetable.StartTime;


                TimeSpan endTime =
                    exception
                        ?.OverrideEndTime
                    ??
                    timetable.EndTime;


                // ==============================================
                // ACTUAL ROOM
                // ==============================================

                string roomName =
                    timetable.Classroom
                        ?.RoomName
                    ??
                    "-";


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


                // ==============================================
                // ADD OCCURRENCE
                // ==============================================

                allClasses.Add(
                    new TeacherAttendanceCodeItemViewModel
                    {
                        TimetableId =
                            timetable.TimetableId,

                        ReplacementId =
                            null,

                        ClassDate =
                            occurrenceDate.Date,

                        CourseName =
                            timetable.Course
                                ?.CourseName
                            ??
                            timetable.CourseId,

                        ClassName =
                            timetable.ClassGroup
                                ?.ClassName
                            ??
                            timetable.ClassId,

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


                occurrenceDate =
                    occurrenceDate
                        .AddDays(7);
            }
        }


        // ==================================================
        // REPLACEMENT CLASSES
        // ==================================================

        foreach (
            ReplacementSession replacement
            in replacements
        )
        {
            Timetable? source =
                replacement.SourceTimetable;


            if (source == null)
            {
                continue;
            }


            // ==============================================
            // REPLACEMENT ROOM
            // ==============================================

            string roomName =
                source.Classroom
                    ?.RoomName
                ??
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


            // ==============================================
            // ADD REPLACEMENT
            // ==============================================

            allClasses.Add(
                new TeacherAttendanceCodeItemViewModel
                {
                    TimetableId =
                        source.TimetableId,

                    ReplacementId =
                        replacement.ReplacementId,

                    ClassDate =
                        replacement
                            .ReplacementDate
                            .Date,

                    CourseName =
                        source.Course
                            ?.CourseName
                        ??
                        source.CourseId,

                    ClassName =
                        source.ClassGroup
                            ?.ClassName
                        ??
                        source.ClassId,

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
        // REMOVE ACCIDENTAL DUPLICATES
        // ==================================================

        allClasses =
            allClasses
                .GroupBy(c =>
                    $"{c.TimetableId}|" +
                    $"{c.ClassDate:yyyyMMdd}|" +
                    $"{c.StartTime}|" +
                    $"{c.ReplacementId}"
                )
                .Select(g =>
                    g.First()
                )
                .ToList();


        // ==================================================
        // SORT
        //
        // Newest date first.
        // On same date -> earliest class first.
        // ==================================================

        allClasses =
            allClasses
                .OrderByDescending(c =>
                    c.ClassDate
                )
                .ThenBy(c =>
                    c.StartTime
                )
                .ToList();


        // ==================================================
        // AVAILABLE DATES
        // ==================================================

        List<DateTime> availableDates =
            allClasses
                .Select(c =>
                    c.ClassDate.Date
                )
                .Distinct()
                .OrderByDescending(d =>
                    d
                )
                .ToList();


        // ==================================================
        // ALWAYS ALLOW "TODAY"
        //
        // Even if teacher has no classes today,
        // Today can still be selected and display empty state.
        // ==================================================

        if (
            today >= rangeStart &&
            today <= rangeEnd &&
            !availableDates.Contains(
                today
            )
        )
        {
            availableDates.Insert(
                0,
                today
            );
        }


        model.AvailableDates =
            availableDates;


        model.TotalItems =
            allClasses.Count;


        // ==================================================
        // SELECT DATE
        // ==================================================

        DateTime selectedDate;


        if (
            date.HasValue &&
            availableDates.Any(d =>
                d.Date ==
                date.Value.Date
            )
        )
        {
            selectedDate =
                date.Value.Date;
        }
        else if (
            availableDates.Any(d =>
                d.Date ==
                today
            )
        )
        {
            selectedDate =
                today;
        }
        else if (
            availableDates.Count >
            0
        )
        {
            selectedDate =
                availableDates[0];
        }
        else
        {
            selectedDate =
                today;
        }


        model.SelectedDate =
            selectedDate;


        model.Date =
            selectedDate;


        // ==================================================
        // SELECTED DATE CLASSES
        //
        // IMPORTANT:
        // No page size.
        // Show ALL classes belonging to selected date.
        // ==================================================

        model.Classes =
            allClasses
                .Where(c =>
                    c.ClassDate.Date ==
                    selectedDate
                )
                .OrderBy(c =>
                    c.StartTime
                )
                .ToList();


        // Old pagination is no longer used.
        model.CurrentPage =
            1;

        model.PageSize =
            model.Classes.Count;

        model.TotalPages =
            availableDates.Count;


        // ==================================================
        // ATTENDANCE SESSIONS FOR SELECTED DATE
        // ==================================================

        List<AttendanceSession> sessions =
            new();


        if (
            model.Classes.Count >
            0
        )
        {
            List<string> selectedTimetableIds =
                model.Classes
                    .Select(c =>
                        c.TimetableId
                    )
                    .Distinct()
                    .ToList();


            DateTime nextDate =
                selectedDate
                    .AddDays(1);


            sessions =
                await db.AttendanceSessions
                    .AsNoTracking()
                    .Where(s =>
                        selectedTimetableIds.Contains(
                            s.TimetableId
                        ) &&

                        s.SessionDate >=
                            selectedDate &&

                        s.SessionDate <
                            nextDate
                    )
                    .ToListAsync();
        }


        // ==================================================
        // BUTTON STATUS
        // ==================================================

        foreach (
            TeacherAttendanceCodeItemViewModel item
            in model.Classes
        )
        {
            // ==============================================
            // FIND SESSION
            // ==============================================

            AttendanceSession? session =
                sessions
                    .Where(s =>
                        s.TimetableId ==
                            item.TimetableId &&

                        s.SessionDate.Date ==
                            item.ClassDate.Date
                    )
                    .OrderByDescending(s =>
                        s.GeneratedAt
                    )
                    .FirstOrDefault();


            // ==============================================
            // CODE WAS GENERATED BEFORE
            // ==============================================

            item.HasGeneratedCode =
                session != null;


            DateTime classStart =
                item.ClassDate
                    .Date
                    .Add(
                        item.StartTime
                    );


            DateTime classEnd =
                item.ClassDate
                    .Date
                    .Add(
                        item.EndTime
                    );


            bool classStarted =
                now >=
                classStart;


            bool classEnded =
                now >=
                classEnd;


            // ==============================================
            // VIEW STUDENTS
            //
            // Code generated:
            // available after class starts.
            //
            // No code:
            // available after class ends for manual attendance.
            // ==============================================

            item.CanViewStudents =
                item.HasGeneratedCode

                    ? classStarted

                    : classEnded;


            // ==============================================
            // MANUAL ATTENDANCE WARNING
            // ==============================================

            item.NeedsManualAttendance =
                classEnded &&
                !item.HasGeneratedCode;


            // ==============================================
            // ACTIVE CODE
            // ==============================================

            bool codeStillActive =
                session != null &&
                session.IsActive &&
                session.ExpiresAt >
                    now;


            if (codeStillActive)
            {
                item.ActiveCode =
                    session!.Code;


                item.ExpiresAt =
                    session.ExpiresAt;


                item.CanGenerate =
                    false;


                item.DisabledReason =
                    "Code already generated.";


                continue;
            }


            // ==============================================
            // CLASS HAS NOT STARTED YET
            // ==============================================

            if (!classStarted)
            {
                item.CanGenerate =
                    false;


                item.DisabledReason =
                    $"Available at " +
                    $"{item.StartTime:hh\\:mm}.";


                continue;
            }


            // ==============================================
            // CLASS ALREADY ENDED
            // ==============================================

            if (classEnded)
            {
                item.CanGenerate =
                    false;


                item.DisabledReason =
                    item.HasGeneratedCode

                        ? "Class has ended."

                        : "Attendance code was not generated. Please complete attendance manually.";


                continue;
            }


            // ==============================================
            // CURRENT CLASS
            // PREVIOUS CODE EXPIRED
            // ==============================================

            if (session != null)
            {
                item.CanGenerate =
                    true;


                item.DisabledReason =
                    "Previous code expired. Generate a new code if needed.";


                continue;
            }


            // ==============================================
            // CURRENT CLASS
            // CAN GENERATE
            // ==============================================

            item.CanGenerate =
                true;


            item.DisabledReason =
                null;
        }


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


        bool hasTeacherProgram =
            await db.TeacherPrograms
                .AnyAsync(tp =>
                    tp.UserId ==
                    userId
                );


        bool hasTeacherCourse =
            await db.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId ==
                    userId
                );


        if (
            !hasTeacherProgram ||
            !hasTeacherCourse
        )
        {
            return RedirectToAction(
                nameof(Create)
            );
        }


        return View(teacher);
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