using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly DB _context;

    private readonly ITimetableValidationService
        _validationService;
    private readonly IAvatarService _avatarService;

    public AdminController(
        DB context,
        ITimetableValidationService validationService,
        IAvatarService avatarService)
    {
        _context = context;
        _validationService = validationService;
        _avatarService = avatarService;
    }

    public async Task<IActionResult> Index()
    {
        string? userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.UserId == userId
            );

        if (user == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        int passkeyCount =
            await _context.UserPasskeys.CountAsync(
                p => p.UserId == user.UserId
            );

        AdminSecurity? adminSecurity =
            await _context.AdminSecurities
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.UserId == user.UserId
                );

        user.Avatar = _avatarService.ResolveAvatar(user.UserId, user.Avatar);

        ViewBag.PasskeyCount = passkeyCount;

        ViewBag.AdminPinEnabled =
            adminSecurity?.PinEnabled == true;

        return View(user);
    }

    // ==================================================
    // DASHBOARD - overall payment overview
    // ==================================================

    /// <summary>
    /// Returns only the results table + pager, for the AJAX refresh.
    /// Takes the same arguments as Dashboard and shares its builder, so
    /// the two can never drift apart.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PaymentsTable(
        string? search,
        string? status,
        string? method,
        DateTime? from,
        DateTime? to,
        int page = 1)
    {
        AdminDashboardViewModel model =
            await BuildDashboardAsync(
                search, status, method, from, to, page);

        return PartialView("_PaymentsTable", model);
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(
        string? search,
        string? status,
        string? method,
        DateTime? from,
        DateTime? to,
        int page = 1)
    {
        AdminDashboardViewModel model =
            await BuildDashboardAsync(
                search, status, method, from, to, page);

        return View(model);
    }

    /// <summary>
    /// Downloads the Overall Student Payments table as CSV, honouring
    /// whatever filters are currently applied. Not paged: the export
    /// contains every matching row, not just the visible page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportPaymentsCsv(
        string? search,
        string? status,
        string? method,
        DateTime? from,
        DateTime? to)
    {
        AdminDashboardViewModel model =
            await BuildDashboardAsync(
                search, status, method, from, to,
                page: 1,
                pageSize: int.MaxValue);

        StringBuilder csv = new();

        csv.AppendLine(string.Join(',',
        [
            "Transaction ID",
            "User ID",
            "Student",
            "Fee",
            "Payment Method",
            "Amount (RM)",
            "Status",
            "Created",
            "Paid"
        ]));

        foreach (AdminPaymentRow row in model.Payments)
        {
            csv.AppendLine(string.Join(',',
            [
                CsvField(row.PaymentTransactionId),
                CsvField(row.UserId),
                CsvField(row.Username),
                CsvField(row.FeeName),
                CsvField(row.PaymentMethod),
                CsvField(row.Amount.ToString(
                    "0.00", CultureInfo.InvariantCulture)),
                CsvField(row.Status),
                CsvField(row.CreatedAt.ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm")),
                CsvField(row.PaidAt.HasValue
                    ? row.PaidAt.Value.ToLocalTime()
                        .ToString("yyyy-MM-dd HH:mm")
                    : "")
            ]));
        }

        // UTF-8 BOM so Excel opens the file in the right encoding.
        byte[] bytes =
            Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();

        string fileName =
            $"payments-{DateTime.Now:yyyyMMdd-HHmm}.csv";

        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// Quotes a CSV field and doubles any embedded quotes, so a comma or
    /// a quote inside a student name cannot break the column layout.
    /// </summary>
    private static string CsvField(string? value)
    {
        string text = value ?? "";

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private async Task<AdminDashboardViewModel> BuildDashboardAsync(
        string? search,
        string? status,
        string? method,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize = 10)
    {
        IQueryable<PaymentTransaction> allPayments =
            _context.PaymentTransactions.AsNoTracking();

        DateTime monthStart =
            new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc
            );

        AdminDashboardViewModel model = new()
        {
            PageSize = pageSize,

            Search = search,

            StatusFilter =
                string.IsNullOrWhiteSpace(status)
                    ? "All"
                    : status,

            MethodFilter =
                string.IsNullOrWhiteSpace(method)
                    ? "All"
                    : method,

            FromDate = from,

            ToDate = to,

            TotalCollected =
                await allPayments
                    .Where(p => p.Status == "Paid")
                    .SumAsync(p => (decimal?)p.Amount)
                ?? 0m,

            PendingAmount =
                await allPayments
                    .Where(p => p.Status == "Pending")
                    .SumAsync(p => (decimal?)p.Amount)
                ?? 0m,

            CollectedThisMonth =
                await allPayments
                    .Where(p =>
                        p.Status == "Paid" &&
                        p.PaidAt != null &&
                        p.PaidAt >= monthStart)
                    .SumAsync(p => (decimal?)p.Amount)
                ?? 0m,

            TotalTransactions =
                await allPayments.CountAsync(),

            PaidCount =
                await allPayments.CountAsync(
                    p => p.Status == "Paid"),

            PendingCount =
                await allPayments.CountAsync(
                    p => p.Status == "Pending"),

            ExpiredCount =
                await allPayments.CountAsync(
                    p => p.Status == "Expired"),

            PayingStudentCount =
                await allPayments
                    .Where(p => p.Status == "Paid")
                    .Select(p => p.UserId)
                    .Distinct()
                    .CountAsync(),

            TotalStudentCount =
                await _context.Users.CountAsync(
                    u =>
                        u.Position == "Student" &&
                        !u.IsDeleted),

            FeeBreakdown =
                await allPayments
                    .Where(p => p.Status == "Paid")
                    .GroupBy(p => p.FeeName)
                    .Select(g =>
                        new AdminFeeBreakdownRow
                        {
                            FeeName = g.Key,
                            PaidCount = g.Count(),
                            TotalCollected =
                                g.Sum(x => x.Amount)
                        })
                    .OrderByDescending(
                        r => r.TotalCollected)
                    .ToListAsync()
        };

        model.AvailableMethods =
            await allPayments
                .Select(p => p.PaymentMethod)
                .Where(m => m != null && m != "")
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

        // ---- Chart data -------------------------------------------------
        // Pulled into memory once. The paid set is small for a school, and
        // grouping locally avoids fighting EF over date-part translation
        // and UTC-to-local conversion.

        var paidRows = await allPayments
            .Where(p => p.Status == "Paid" && p.PaidAt != null)
            .Select(p => new
            {
                PaidAt = p.PaidAt!.Value,
                p.Amount,
                p.PaymentMethod,
                p.UserId,
                Username = p.User.Username
            })
            .ToListAsync();

        var paidLocal = paidRows
            .Select(r => new
            {
                When = r.PaidAt.ToLocalTime(),
                r.Amount,
                Method = string.IsNullOrWhiteSpace(r.PaymentMethod)
                    ? "Unknown"
                    : r.PaymentMethod,
                r.UserId,
                r.Username
            })
            .ToList();

        // Monthly collection - last 12 calendar months, gaps included.
        DateTime thisMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);

        for (int back = 11; back >= 0; back--)
        {
            DateTime month = thisMonth.AddMonths(-back);

            var inMonth = paidLocal
                .Where(r =>
                    r.When.Year == month.Year &&
                    r.When.Month == month.Month)
                .ToList();

            model.MonthlyCollection.Add(new AdminMonthPoint
            {
                Label = month.ToString("MMM yy"),
                Count = inMonth.Count,
                Amount = inMonth.Sum(r => r.Amount)
            });
        }

        // Payment method mix.
        model.MethodBreakdown = paidLocal
            .GroupBy(r => r.Method)
            .Select(g => new AdminChartSlice
            {
                Label = g.Key,
                Count = g.Count(),
                Amount = g.Sum(r => r.Amount)
            })
            .OrderByDescending(s => s.Amount)
            .ToList();

        // Who pays the most.
        model.TopStudents = paidLocal
            .GroupBy(r => new { r.UserId, r.Username })
            .Select(g => new AdminChartSlice
            {
                Label = string.IsNullOrWhiteSpace(g.Key.Username)
                    ? g.Key.UserId
                    : g.Key.Username,
                Count = g.Count(),
                Amount = g.Sum(r => r.Amount)
            })
            .OrderByDescending(s => s.Amount)
            .Take(10)
            .ToList();

        // Day of week, Monday first.
        DayOfWeek[] weekOrder =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
            DayOfWeek.Sunday
        ];

        foreach (DayOfWeek day in weekOrder)
        {
            var onDay = paidLocal
                .Where(r => r.When.DayOfWeek == day)
                .ToList();

            model.WeekdayBuckets.Add(new AdminBucket
            {
                Label = day.ToString()[..3],
                Count = onDay.Count,
                Amount = onDay.Sum(r => r.Amount)
            });
        }

        // Four-hour blocks across the day.
        for (int startHour = 0; startHour < 24; startHour += 4)
        {
            int endHour = startHour + 4;

            var inBlock = paidLocal
                .Where(r =>
                    r.When.Hour >= startHour &&
                    r.When.Hour < endHour)
                .ToList();

            model.HourBuckets.Add(new AdminBucket
            {
                Label = $"{startHour:00}-{endHour:00}",
                Count = inBlock.Count,
                Amount = inBlock.Sum(r => r.Amount)
            });
        }

        // Headline answers, so each chart has a plain-language summary.
        model.BusiestMonth =
            model.MonthlyCollection.Any(m => m.Amount > 0)
                ? model.MonthlyCollection
                    .OrderByDescending(m => m.Amount)
                    .First().Label
                : "-";

        model.BusiestDay =
            model.WeekdayBuckets.Any(b => b.Count > 0)
                ? model.WeekdayBuckets
                    .OrderByDescending(b => b.Count)
                    .First().Label
                : "-";

        model.BusiestHour =
            model.HourBuckets.Any(b => b.Count > 0)
                ? model.HourBuckets
                    .OrderByDescending(b => b.Count)
                    .First().Label
                : "-";

        model.TopMethod =
            model.MethodBreakdown.Count > 0
                ? model.MethodBreakdown[0].Label
                : "-";

        IQueryable<PaymentTransaction> filtered =
            allPayments;

        if (!string.Equals(
                model.StatusFilter,
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            filtered =
                filtered.Where(
                    p =>
                        p.Status ==
                        model.StatusFilter
                );
        }

        if (!string.Equals(
                model.MethodFilter,
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            filtered =
                filtered.Where(
                    p =>
                        p.PaymentMethod ==
                        model.MethodFilter
                );
        }

        // Dates are stored in UTC but the admin picks local dates, so shift
        // the boundaries before comparing. A row is dated by PaidAt when it
        // exists, matching what the Date column shows.
        if (model.FromDate.HasValue)
        {
            DateTime fromUtc =
                DateTime.SpecifyKind(
                    model.FromDate.Value.Date,
                    DateTimeKind.Local)
                .ToUniversalTime();

            filtered =
                filtered.Where(p =>
                    (p.PaidAt ?? p.CreatedAt) >= fromUtc);
        }

        if (model.ToDate.HasValue)
        {
            // Add a day and use "<" so the end date itself is included.
            DateTime toUtc =
                DateTime.SpecifyKind(
                    model.ToDate.Value.Date.AddDays(1),
                    DateTimeKind.Local)
                .ToUniversalTime();

            filtered =
                filtered.Where(p =>
                    (p.PaidAt ?? p.CreatedAt) < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term =
                search.Trim();

            filtered =
                filtered.Where(p =>
                    p.PaymentTransactionId.Contains(term) ||
                    p.UserId.Contains(term) ||
                    p.User.Username.Contains(term) ||
                    p.FeeName.Contains(term));
        }

        // Paging: count the filtered set first, then fetch only this page.
        model.TotalPayments = await filtered.CountAsync();

        model.TotalPages =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    model.TotalPayments / (double)model.PageSize));

        // Clamp, so a stale or hand-typed ?page= never yields an empty table.
        model.Page = Math.Clamp(page, 1, model.TotalPages);

        model.Payments =
            await filtered
                .OrderByDescending(
                    p => p.CreatedAt)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(p =>
                    new AdminPaymentRow
                    {
                        PaymentTransactionId =
                            p.PaymentTransactionId,

                        UserId =
                            p.UserId,

                        Username =
                            p.User.Username,

                        FeeName =
                            p.FeeName,

                        PaymentMethod =
                            p.PaymentMethod,

                        Amount =
                            p.Amount,

                        Status =
                            p.Status,

                        CreatedAt =
                            p.CreatedAt,

                        PaidAt =
                            p.PaidAt
                    })
                .ToListAsync();

        return model;
    }


    // ==================================================
    // ATTENDANCE OVERVIEW
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> AttendanceOverview()
    {
        DateTime now = DateTime.Now;
        DateTime today = DateTime.Today;

        AdminAttendanceOverviewViewModel model = new();

        // ==================================================
        // ACTIVE TERM
        // ==================================================

        AcademicTerm? activeTerm =
            await _context
                .AcademicTerms
                .AsNoTracking()
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.StartDate)
                .FirstOrDefaultAsync();

        if (activeTerm == null)
        {
            model.TermName = "No Active Semester";
            return View(model);
        }

        model.TermName = activeTerm.Name;

        // ==================================================
        // TERM DATE RANGE
        // ==================================================

        DateTime termStart = activeTerm.StartDate.Date;

        DateTime termEnd =
            activeTerm.EndDate.Date < today
                ? activeTerm.EndDate.Date
                : today;

        if (termStart > termEnd)
        {
            return View(model);
        }

        // ==================================================
        // ALL REGISTERED STUDENTS
        // ==================================================

        List<StudentClass> students =
            await _context
                .StudentClasses
                .AsNoTracking()
                .Include(sc => sc.User)
                .Include(sc => sc.Program)
                .Include(sc => sc.ClassGroup)
                .Where(sc =>
                    sc.User != null &&
                    sc.User.Position == "Student" &&
                    !sc.User.IsDeleted)
                .OrderBy(sc => sc.User!.Username)
                .ToListAsync();

        model.TotalStudents = students.Count;

        if (students.Count == 0)
        {
            return View(model);
        }

        // ==================================================
        // ACTIVE TERM TIMETABLE
        // ==================================================

        List<Timetable> timetables =
            await _context
                .Timetables
                .AsNoTracking()
                .Where(t => t.TermId == activeTerm.TermId)
                .Include(t => t.Term)
                .Include(t => t.Course)
                .Include(t => t.ClassGroup)
                .ToListAsync();

        if (timetables.Count == 0)
        {
            return View(model);
        }

        List<string> timetableIds =
            timetables
                .Select(t => t.TimetableId)
                .Distinct()
                .ToList();

        // ==================================================
        // TIMETABLE EXCEPTIONS
        // ==================================================

        List<TimetableException> exceptions =
            await _context
                .TimetableExceptions
                .AsNoTracking()
                .Where(e =>
                    timetableIds.Contains(e.TimetableId) &&
                    e.OccurrenceDate.Date >= termStart &&
                    e.OccurrenceDate.Date <= termEnd)
                .ToListAsync();

        // ==================================================
        // REPLACEMENT CLASSES
        // ==================================================

        List<ReplacementSession> replacements =
            await _context
                .ReplacementSessions
                .AsNoTracking()
                .Where(r =>
                    timetableIds.Contains(r.SourceTimetableId) &&
                    r.ReplacementDate.Date >= termStart &&
                    r.ReplacementDate.Date <= termEnd &&
                    r.Status == "Scheduled")
                .ToListAsync();

        // ==================================================
        // ATTENDANCE SESSIONS
        // ==================================================

        List<AttendanceSession> sessions =
            await _context
                .AttendanceSessions
                .AsNoTracking()
                .Where(s =>
                    timetableIds.Contains(s.TimetableId) &&
                    s.SessionDate.Date >= termStart &&
                    s.SessionDate.Date <= termEnd)
                .ToListAsync();

        Dictionary<string, AttendanceSession> sessionMap =
            sessions
                .GroupBy(s =>
                    GetAdminAttendanceOccurrenceKey(
                        s.TimetableId,
                        s.SessionDate))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s => s.GeneratedAt)
                        .First());

        // ==================================================
        // ATTENDANCE RECORDS
        // ==================================================

        List<string> sessionIds =
            sessions
                .Select(s => s.AttendanceSessionId)
                .Distinct()
                .ToList();

        List<AttendanceRecord> records =
            sessionIds.Count == 0
                ? new List<AttendanceRecord>()
                : await _context
                    .AttendanceRecords
                    .AsNoTracking()
                    .Where(r =>
                        sessionIds.Contains(r.AttendanceSessionId))
                    .ToListAsync();

        Dictionary<string, AttendanceRecord> recordMap =
            records
                .GroupBy(r =>
                    GetAdminAttendanceRecordKey(
                        r.AttendanceSessionId,
                        r.StudentClassId))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(r => r.SubmittedAt)
                        .First());

        // ==================================================
        // APPROVED LEAVES
        // ==================================================

        List<string> studentUserIds =
            students
                .Select(sc => sc.UserId)
                .Distinct()
                .ToList();

        List<LeaveApplication> approvedLeaves =
            await _context
                .LeaveApplications
                .AsNoTracking()
                .Where(l =>
                    studentUserIds.Contains(l.StudentId) &&
                    l.Status == "Approved" &&
                    l.EndDate.Date >= termStart &&
                    l.StartDate.Date <= termEnd)
                .ToListAsync();

        // ==================================================
        // SUBJECT MAP
        // ==================================================

        Dictionary<string, AdminAttendanceSubjectViewModel> subjectMap =
            timetables
                .GroupBy(t => t.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        Timetable timetable = g.First();

                        return new AdminAttendanceSubjectViewModel
                        {
                            CourseId = timetable.CourseId,
                            CourseCode = timetable.Course?.CourseCode ?? "",
                            CourseName =
                                timetable.Course?.CourseName
                                ?? timetable.CourseId
                        };
                    });

        // ==================================================
        // SUBJECT STUDENT SETS
        // ==================================================

        Dictionary<string, HashSet<string>> subjectStudents = new();

        // Used for Overall Attendance +4 buffer.
        HashSet<string> studentSubjectPairs = new();

        // ==================================================
        // ABSENCE BY DAY
        // ==================================================

        Dictionary<DayOfWeek, int> absenceByDay =
            new()
            {
                [DayOfWeek.Monday] = 0,
                [DayOfWeek.Tuesday] = 0,
                [DayOfWeek.Wednesday] = 0,
                [DayOfWeek.Thursday] = 0,
                [DayOfWeek.Friday] = 0,
                [DayOfWeek.Saturday] = 0,
                [DayOfWeek.Sunday] = 0
            };

        // ==================================================
        // LOOP THROUGH EVERY STUDENT
        // ==================================================

        foreach (StudentClass student in students)
        {
            List<Timetable> studentTimetables =
                timetables
                    .Where(t => t.ClassId == student.ClassId)
                    .ToList();

            if (studentTimetables.Count == 0)
            {
                continue;
            }

            List<string> studentTimetableIds =
                studentTimetables
                    .Select(t => t.TimetableId)
                    .ToList();

            List<TimetableException> studentExceptions =
                exceptions
                    .Where(e =>
                        studentTimetableIds.Contains(e.TimetableId))
                    .ToList();

            List<ReplacementSession> studentReplacements =
                replacements
                    .Where(r =>
                        studentTimetableIds.Contains(
                            r.SourceTimetableId))
                    .ToList();

            List<AdminAttendanceOccurrence> occurrences =
                BuildAdminAttendanceOccurrences(
                    student,
                    studentTimetables,
                    studentExceptions,
                    studentReplacements,
                    activeTerm,
                    today);

            // Only completed classes count in analytics.
            occurrences =
                occurrences
                    .Where(o =>
                        now >= o.Date.Add(o.EndTime))
                    .ToList();

            foreach (AdminAttendanceOccurrence occurrence in occurrences)
            {
                if (!subjectMap.ContainsKey(occurrence.CourseId))
                {
                    continue;
                }

                if (!subjectStudents.ContainsKey(occurrence.CourseId))
                {
                    subjectStudents[occurrence.CourseId] =
                        new HashSet<string>();
                }

                subjectStudents[occurrence.CourseId]
                    .Add(student.StudentClassId);

                studentSubjectPairs.Add(
                    $"{student.StudentClassId}|{occurrence.CourseId}");

                string occurrenceKey =
                    GetAdminAttendanceOccurrenceKey(
                        occurrence.TimetableId,
                        occurrence.Date);

                sessionMap.TryGetValue(
                    occurrenceKey,
                    out AttendanceSession? session);

                AttendanceRecord? record = null;

                if (session != null)
                {
                    string recordKey =
                        GetAdminAttendanceRecordKey(
                            session.AttendanceSessionId,
                            student.StudentClassId);

                    recordMap.TryGetValue(
                        recordKey,
                        out record);
                }

                LeaveApplication? approvedLeave =
                    approvedLeaves
                        .FirstOrDefault(l =>
                            l.StudentId == student.UserId &&
                            occurrence.Date >= l.StartDate.Date &&
                            occurrence.Date <= l.EndDate.Date);

                string status =
                    ResolveAdminAttendanceStatus(
                        occurrence,
                        record,
                        approvedLeave,
                        now);

                AdminAttendanceSubjectViewModel subject =
                    subjectMap[occurrence.CourseId];

                if (status == "Present")
                {
                    subject.PresentCount++;
                    model.TotalPresent++;
                }
                else if (status == "Leave")
                {
                    subject.LeaveCount++;
                    model.TotalLeave++;
                }
                else if (status == "Absent")
                {
                    subject.AbsentCount++;
                    model.TotalAbsent++;

                    absenceByDay[occurrence.Date.DayOfWeek]++;
                }
            }
        }

        // ==================================================
        // ATTENDANCE FORMULA
        //
        // (Present + 4) / (Present + Absent + 4) x 100
        // Approved Leave is excluded.
        // ==================================================

        const int AttendanceStartingBuffer = 4;

        // ==================================================
        // SUBJECT ATTENDANCE RATE
        // ==================================================

        foreach (
            KeyValuePair<string, AdminAttendanceSubjectViewModel> pair
            in subjectMap)
        {
            AdminAttendanceSubjectViewModel subject = pair.Value;

            int studentCount =
                subjectStudents.TryGetValue(
                    pair.Key,
                    out HashSet<string>? set)
                        ? set.Count
                        : 0;

            subject.StudentCount = studentCount;

            int countedClasses =
                subject.PresentCount +
                subject.AbsentCount;

            int subjectBuffer =
                studentCount *
                AttendanceStartingBuffer;

            subject.AttendanceRate =
                countedClasses == 0
                    ? 100
                    : Math.Round(
                        (subject.PresentCount + subjectBuffer) *
                        100.0 /
                        (
                            subject.PresentCount +
                            subject.AbsentCount +
                            subjectBuffer
                        ),
                        1);
        }

        // ==================================================
        // ALL SUBJECTS
        // ==================================================

        model.Subjects =
            subjectMap
                .Values
                .Where(s => s.StudentCount > 0)
                .OrderBy(s => s.CourseName)
                .ToList();

        // ==================================================
        // TOP 5 MOST ABSENT SUBJECTS
        // ==================================================

        model.TopAbsentSubjects =
            model.Subjects
                .Where(s => s.AbsentCount > 0)
                .OrderByDescending(s => s.AbsentCount)
                .ThenBy(s => s.AttendanceRate)
                .Take(5)
                .ToList();

        // ==================================================
        // OVERALL ATTENDANCE
        // ==================================================

        int totalCounted =
            model.TotalPresent +
            model.TotalAbsent;

        int overallBuffer =
            studentSubjectPairs.Count *
            AttendanceStartingBuffer;

        model.OverallAttendanceRate =
            totalCounted == 0
                ? 100
                : Math.Round(
                    (model.TotalPresent + overallBuffer) *
                    100.0 /
                    (
                        model.TotalPresent +
                        model.TotalAbsent +
                        overallBuffer
                    ),
                    1);

        // ==================================================
        // ABSENCE BY DAY
        // ==================================================

        model.AbsenceByDay =
            new List<AdminAttendanceDayViewModel>
            {
                new()
                {
                    DayName = "Mon",
                    AbsentCount = absenceByDay[DayOfWeek.Monday]
                },
                new()
                {
                    DayName = "Tue",
                    AbsentCount = absenceByDay[DayOfWeek.Tuesday]
                },
                new()
                {
                    DayName = "Wed",
                    AbsentCount = absenceByDay[DayOfWeek.Wednesday]
                },
                new()
                {
                    DayName = "Thu",
                    AbsentCount = absenceByDay[DayOfWeek.Thursday]
                },
                new()
                {
                    DayName = "Fri",
                    AbsentCount = absenceByDay[DayOfWeek.Friday]
                },
                new()
                {
                    DayName = "Sat",
                    AbsentCount = absenceByDay[DayOfWeek.Saturday]
                },
                new()
                {
                    DayName = "Sun",
                    AbsentCount = absenceByDay[DayOfWeek.Sunday]
                }
            };

        return View(model);
    }

    // ==================================================
    // BUILD ADMIN ATTENDANCE OCCURRENCES
    // ==================================================

    private static List<AdminAttendanceOccurrence>
        BuildAdminAttendanceOccurrences(
            StudentClass student,
            List<Timetable> timetables,
            List<TimetableException> exceptions,
            List<ReplacementSession> replacements,
            AcademicTerm term,
            DateTime today)
    {
        List<AdminAttendanceOccurrence> result = new();

        DateTime fromDate =
            student.RegisteredDate.Date > term.StartDate.Date
                ? student.RegisteredDate.Date
                : term.StartDate.Date;

        DateTime toDate =
            today < term.EndDate.Date
                ? today
                : term.EndDate.Date;

        if (fromDate > toDate)
        {
            return result;
        }

        // ==================================================
        // NORMAL TIMETABLE CLASSES
        // ==================================================

        foreach (Timetable timetable in timetables)
        {
            int dayOffset =
                (
                    (int)timetable.DayOfWeek -
                    (int)fromDate.DayOfWeek +
                    7
                ) % 7;

            DateTime occurrenceDate =
                fromDate.AddDays(dayOffset);

            while (occurrenceDate <= toDate)
            {
                TimetableException? exception =
                    exceptions
                        .FirstOrDefault(e =>
                            e.TimetableId == timetable.TimetableId &&
                            e.OccurrenceDate.Date == occurrenceDate.Date &&
                            !string.Equals(
                                e.Decision,
                                "Pending",
                                StringComparison.OrdinalIgnoreCase));

                bool cancelled =
                    exception != null &&
                    IsAdminAttendanceCancelledValue(
                        exception.Decision);

                bool movedToReplacement =
                    replacements
                        .Any(r =>
                            r.SourceTimetableId ==
                                timetable.TimetableId &&
                            r.SourceOccurrenceDate.HasValue &&
                            r.SourceOccurrenceDate.Value.Date ==
                                occurrenceDate.Date);

                if (!cancelled && !movedToReplacement)
                {
                    TimeSpan actualStart =
                        exception?.OverrideStartTime
                        ?? timetable.StartTime;

                    TimeSpan actualEnd =
                        exception?.OverrideEndTime
                        ?? timetable.EndTime;

                    DateTime classStart =
                        occurrenceDate.Add(actualStart);

                    if (student.RegisteredDate <= classStart)
                    {
                        result.Add(
                            new AdminAttendanceOccurrence
                            {
                                TimetableId = timetable.TimetableId,
                                CourseId = timetable.CourseId,
                                CourseCode =
                                    timetable.Course?.CourseCode ?? "",
                                CourseName =
                                    timetable.Course?.CourseName
                                    ?? timetable.CourseId,
                                Date = occurrenceDate.Date,
                                StartTime = actualStart,
                                EndTime = actualEnd,
                                IsReplacement = false
                            });
                    }
                }

                occurrenceDate =
                    occurrenceDate.AddDays(7);
            }
        }

        // ==================================================
        // REPLACEMENT CLASSES
        // ==================================================

        foreach (ReplacementSession replacement in replacements)
        {
            Timetable? source =
                timetables
                    .FirstOrDefault(t =>
                        t.TimetableId ==
                            replacement.SourceTimetableId);

            if (source == null)
            {
                continue;
            }

            DateTime replacementDate =
                replacement.ReplacementDate.Date;

            if (
                replacementDate < fromDate ||
                replacementDate > toDate)
            {
                continue;
            }

            DateTime replacementStart =
                replacementDate.Add(
                    replacement.StartTime);

            if (student.RegisteredDate > replacementStart)
            {
                continue;
            }

            result.Add(
                new AdminAttendanceOccurrence
                {
                    TimetableId = source.TimetableId,
                    CourseId = source.CourseId,
                    CourseCode = source.Course?.CourseCode ?? "",
                    CourseName =
                        source.Course?.CourseName
                        ?? source.CourseId,
                    Date = replacementDate,
                    StartTime = replacement.StartTime,
                    EndTime = replacement.EndTime,
                    IsReplacement = true
                });
        }

        // ==================================================
        // REMOVE DUPLICATE OCCURRENCES
        // ==================================================

        return result
            .GroupBy(o =>
                $"{o.TimetableId}|" +
                $"{o.Date:yyyyMMdd}|" +
                $"{o.StartTime}")
            .Select(g => g.First())
            .OrderBy(o => o.Date)
            .ThenBy(o => o.StartTime)
            .ToList();
    }

    // ==================================================
    // RESOLVE ATTENDANCE STATUS
    // ==================================================

    private static string ResolveAdminAttendanceStatus(
        AdminAttendanceOccurrence occurrence,
        AttendanceRecord? record,
        LeaveApplication? approvedLeave,
        DateTime now)
    {
        // 1. APPROVED LEAVE
        if (approvedLeave != null)
        {
            return "Leave";
        }

        // 2. VALID PRESENT RECORD
        if (
            record != null &&
            !record.IsRemoved &&
            !string.Equals(
                record.Status,
                "Absent",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Present";
        }

        // 3. REMOVED / ABSENT RECORD
        if (
            record != null &&
            (
                record.IsRemoved ||
                string.Equals(
                    record.Status,
                    "Absent",
                    StringComparison.OrdinalIgnoreCase)
            ))
        {
            return "Absent";
        }

        // 4. CLASS FINISHED + NO PRESENT RECORD = ABSENT
        DateTime classEnd =
            occurrence.Date.Add(
                occurrence.EndTime);

        if (now >= classEnd)
        {
            return "Absent";
        }

        return "Not Open";
    }

    // ==================================================
    // OCCURRENCE KEY
    // ==================================================

    private static string GetAdminAttendanceOccurrenceKey(
        string timetableId,
        DateTime date)
    {
        return
            $"{timetableId}|" +
            $"{date.Date:yyyyMMdd}";
    }

    // ==================================================
    // ATTENDANCE RECORD KEY
    // ==================================================

    private static string GetAdminAttendanceRecordKey(
        string attendanceSessionId,
        string studentClassId)
    {
        return
            $"{attendanceSessionId}|" +
            $"{studentClassId}";
    }

    // ==================================================
    // CANCELLED VALUE
    // ==================================================

    private static bool IsAdminAttendanceCancelledValue(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value
            .Trim()
            .Contains(
                "cancel",
                StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================
    // INTERNAL ATTENDANCE OCCURRENCE
    // ==================================================

    private class AdminAttendanceOccurrence
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


    // ==================================================
    // LEAVE MANAGEMENT
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> LeaveManagement(
        int page = 1)
    {
        const int pageSize = 10;

        int totalCount =
            await _context
                .LeaveApplications
                .CountAsync();

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

        var leaves =
            await _context
                .LeaveApplications
                .AsNoTracking()
                .Include(l => l.Student)
                .OrderByDescending(
                    l => l.AppliedAt)
                .Skip(
                    (page - 1) *
                    pageSize
                )
                .Take(pageSize)
                .ToListAsync();

        ViewBag.CurrentPage =
            page;

        ViewBag.TotalPages =
            totalPages;

        ViewBag.TotalCount =
            totalCount;

        ViewBag.PageSize =
            pageSize;

        return View(leaves);
    }

    // ==================================================
    // APPROVE LEAVE
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLeave(
        int id)
    {
        LeaveApplication? leave =
            await _context
                .LeaveApplications
                .FirstOrDefaultAsync(
                    l =>
                        l.LeaveId ==
                        id
                );

        if (leave == null)
        {
            return NotFound();
        }

        if (!string.Equals(
                leave.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["LeaveError"] =
                "Only pending leave applications can be approved.";

            return RedirectToAction(
                nameof(LeaveManagement)
            );
        }

        leave.Status =
            "Approved";

        await _context
            .SaveChangesAsync();

        TempData["LeaveSuccess"] =
            "Leave application approved successfully.";

        return RedirectToAction(
            nameof(LeaveManagement)
        );
    }

    // ==================================================
    // REJECT LEAVE
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectLeave(
        int id)
    {
        LeaveApplication? leave =
            await _context
                .LeaveApplications
                .FirstOrDefaultAsync(
                    l =>
                        l.LeaveId ==
                        id
                );

        if (leave == null)
        {
            return NotFound();
        }

        if (!string.Equals(
                leave.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["LeaveError"] =
                "Only pending leave applications can be rejected.";

            return RedirectToAction(
                nameof(LeaveManagement)
            );
        }

        leave.Status =
            "Rejected";

        await _context
            .SaveChangesAsync();

        TempData["LeaveSuccess"] =
            "Leave application rejected successfully.";

        return RedirectToAction(
            nameof(LeaveManagement)
        );
    }

    // ==================================================
    // BARRED LIST
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> BarredList(
        int barredPage = 1,
        int eligiblePage = 1)
    {
        const int pageSize = 5;

        // ==========================================
        // GET ALL REGISTERED STUDENTS
        // ==========================================

        List<StudentClass> studentClasses =
            await _context
                .StudentClasses
                .AsNoTracking()
                .Include(sc => sc.User)
                .Include(sc => sc.Program)
                .Where(sc =>
                    sc.User != null &&
                    sc.User.Position == "Student" &&
                    !sc.User.IsDeleted
                )
                .OrderBy(sc =>
                    sc.User!.Username
                )
                .ToListAsync();


        // ==========================================
        // GET CURRENT BARRED RECORDS
        // ==========================================

        List<BarredStudent> barredRecords =
            await _context
                .BarredStudents
                .AsNoTracking()
                .Include(b => b.Student)
                .Include(b => b.Course)
                .OrderByDescending(b =>
                    b.BarredAt
                )
                .ToListAsync();


        // ==========================================
        // BUILD FULL CURRENT BARRED LIST
        // ==========================================

        List<BarredStudentRowViewModel> allBarredStudents =
            new();


        foreach (
            BarredStudent barred
            in barredRecords)
        {
            StudentClass? studentClass =
                studentClasses
                    .FirstOrDefault(sc =>
                        sc.UserId ==
                        barred.UserId
                    );

            if (
                studentClass == null ||
                barred.Student == null ||
                barred.Course == null
            )
            {
                continue;
            }


            AttendanceCalculationResult result =
                await CalculateAttendanceAsync(
                    studentClass,
                    barred.CourseId
                );


            allBarredStudents.Add(
                new BarredStudentRowViewModel
                {
                    BarredStudentId =
                        barred.BarredStudentId,

                    UserId =
                        barred.UserId,

                    StudentId =
                        studentClass.StudentId,

                    Username =
                        barred.Student.Username,

                    ProgramName =
                        studentClass.Program
                            ?.ProgramName
                        ?? studentClass.ProgramId,

                    CourseId =
                        barred.CourseId,

                    CourseCode =
                        barred.Course.CourseCode
                        ?? "",

                    CourseName =
                        barred.Course.CourseName,

                    AbsentCount =
                        result.AbsentCount,

                    AttendancePercentage =
                        result.Percentage,

                    Reason =
                        barred.Reason,

                    BarredAt =
                        barred.BarredAt
                }
            );
        }


        // ==========================================
        // BUILD FULL ELIGIBLE LIST
        // ==========================================

        List<BarredCandidateViewModel> allEligibleStudents =
            new();


        foreach (
            StudentClass studentClass
            in studentClasses)
        {
            List<ProgramCourse> programCourses =
                await _context
                    .ProgramCourses
                    .AsNoTracking()
                    .Where(pc =>
                        pc.ProgramId ==
                        studentClass.ProgramId
                    )
                    .Include(pc =>
                        pc.Course
                    )
                    .ToListAsync();


            foreach (
                ProgramCourse programCourse
                in programCourses)
            {
                if (
                    programCourse.Course ==
                    null
                )
                {
                    continue;
                }


                AttendanceCalculationResult result =
                    await CalculateAttendanceAsync(
                        studentClass,
                        programCourse.CourseId
                    );


                double displayPercentage =
                    Math.Round(
                        result.Percentage,
                        1
                    );


                // Only students with 80% or below
                // are eligible for the barred list.
                if (
                    displayPercentage >
                    80
                )
                {
                    continue;
                }


                bool alreadyBarred =
                    barredRecords.Any(b =>
                        b.UserId ==
                            studentClass.UserId &&
                        b.CourseId ==
                            programCourse.CourseId
                    );


                if (
                    alreadyBarred
                )
                {
                    continue;
                }


                allEligibleStudents.Add(
                    new BarredCandidateViewModel
                    {
                        UserId =
                            studentClass.UserId,

                        StudentId =
                            studentClass.StudentId,

                        Username =
                            studentClass.User
                                ?.Username
                            ?? studentClass.UserId,

                        ProgramName =
                            studentClass.Program
                                ?.ProgramName
                            ?? studentClass.ProgramId,

                        CourseId =
                            programCourse.CourseId,

                        CourseCode =
                            programCourse.Course
                                .CourseCode
                            ?? "",

                        CourseName =
                            programCourse.Course
                                .CourseName,

                        AbsentCount =
                            result.AbsentCount,

                        AttendancePercentage =
                            result.Percentage
                    }
                );
            }
        }


        allEligibleStudents =
            allEligibleStudents
                .OrderBy(x =>
                    x.AttendancePercentage
                )
                .ThenBy(x =>
                    x.Username
                )
                .ToList();


        // ==========================================
        // CURRENT BARRED PAGINATION
        // ==========================================

        int barredTotalCount =
            allBarredStudents.Count;

        int barredTotalPages =
            (int)Math.Ceiling(
                barredTotalCount /
                (double)pageSize
            );

        if (
            barredTotalPages <
            1
        )
        {
            barredTotalPages =
                1;
        }

        barredPage =
            Math.Clamp(
                barredPage,
                1,
                barredTotalPages
            );


        // ==========================================
        // ELIGIBLE PAGINATION
        // ==========================================

        int eligibleTotalCount =
            allEligibleStudents.Count;

        int eligibleTotalPages =
            (int)Math.Ceiling(
                eligibleTotalCount /
                (double)pageSize
            );

        if (
            eligibleTotalPages <
            1
        )
        {
            eligibleTotalPages =
                1;
        }

        eligiblePage =
            Math.Clamp(
                eligiblePage,
                1,
                eligibleTotalPages
            );


        // ==========================================
        // BUILD PAGE MODEL
        // ==========================================

        BarredListViewModel model =
            new()
            {
                BarredStudents =
                    allBarredStudents
                        .Skip(
                            (barredPage - 1) *
                            pageSize
                        )
                        .Take(pageSize)
                        .ToList(),

                EligibleStudents =
                    allEligibleStudents
                        .Skip(
                            (eligiblePage - 1) *
                            pageSize
                        )
                        .Take(pageSize)
                        .ToList()
            };


        // ==========================================
        // SEND PAGINATION DATA TO VIEW
        // ==========================================

        ViewBag.BarredPage =
            barredPage;

        ViewBag.BarredTotalPages =
            barredTotalPages;

        ViewBag.BarredTotalCount =
            barredTotalCount;

        ViewBag.EligiblePage =
            eligiblePage;

        ViewBag.EligibleTotalPages =
            eligibleTotalPages;

        ViewBag.EligibleTotalCount =
            eligibleTotalCount;

        ViewBag.PageSize =
            pageSize;


        return View(model);
    }


    // ==================================================
    // ADD STUDENT TO BARRED LIST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBarredStudent(
        string userId,
        string courseId,
        string? reason)
    {
        if (
            string.IsNullOrWhiteSpace(
                userId
            ) ||
            string.IsNullOrWhiteSpace(
                courseId
            )
        )
        {
            TempData["BarredError"] =
                "Student and subject are required.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        StudentClass? studentClass =
            await _context
                .StudentClasses
                .Include(sc =>
                    sc.User
                )
                .Include(sc =>
                    sc.Program
                )
                .FirstOrDefaultAsync(sc =>
                    sc.UserId ==
                        userId &&
                    sc.User != null &&
                    sc.User.Position ==
                        "Student" &&
                    !sc.User.IsDeleted
                );

        if (
            studentClass ==
            null
        )
        {
            TempData["BarredError"] =
                "Student registration could not be found.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        ProgramCourse? mapping =
            await _context
                .ProgramCourses
                .AsNoTracking()
                .Include(pc =>
                    pc.Course
                )
                .FirstOrDefaultAsync(pc =>
                    pc.ProgramId ==
                        studentClass.ProgramId &&
                    pc.CourseId ==
                        courseId
                );

        if (
            mapping == null ||
            mapping.Course == null
        )
        {
            TempData["BarredError"] =
                "This subject does not belong to the student's program.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        bool alreadyBarred =
            await _context
                .BarredStudents
                .AnyAsync(b =>
                    b.UserId ==
                        userId &&
                    b.CourseId ==
                        courseId
                );

        if (
            alreadyBarred
        )
        {
            TempData["BarredError"] =
                "This student is already barred for this subject.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        AttendanceCalculationResult result =
            await CalculateAttendanceAsync(
                studentClass,
                courseId
            );

        double displayPercentage =
            Math.Round(
                result.Percentage,
                1
            );

        if (
            displayPercentage >
            80
        )
        {
            TempData["BarredError"] =
                "This student cannot be barred because attendance is above 80%.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        string finalReason =
            string.IsNullOrWhiteSpace(
                reason
            )
                ? $"Low attendance ({displayPercentage:0.#}%)."
                : reason.Trim();

        if (
            finalReason.Length >
            300
        )
        {
            TempData["BarredError"] =
                "Reason cannot exceed 300 characters.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        BarredStudent barredStudent =
            new()
            {
                BarredStudentId =
                    await FormattedIdGenerator
                        .NextAsync(
                            _context.BarredStudents,
                            b =>
                                b.BarredStudentId,
                            "BRD"
                        ),

                UserId =
                    userId,

                CourseId =
                    courseId,

                Reason =
                    finalReason,

                BarredAt =
                    DateTime.Now
            };

        _context
            .BarredStudents
            .Add(
                barredStudent
            );

        await _context
            .SaveChangesAsync();

        TempData["BarredSuccess"] =
            $"{studentClass.User?.Username ?? studentClass.StudentId} " +
            $"was added to the barred list for " +
            $"{mapping.Course.CourseName}.";

        return RedirectToAction(
            nameof(BarredList)
        );
    }

    // ==================================================
    // EDIT BARRED REASON
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBarredStudent(
        string id,
        string reason)
    {
        BarredStudent? barredStudent =
            await _context
                .BarredStudents
                .FirstOrDefaultAsync(b =>
                    b.BarredStudentId ==
                    id
                );

        if (
            barredStudent ==
            null
        )
        {
            TempData["BarredError"] =
                "Barred record could not be found.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        reason =
            (
                reason ??
                ""
            )
            .Trim();

        if (
            string.IsNullOrWhiteSpace(
                reason
            )
        )
        {
            TempData["BarredError"] =
                "Reason is required.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        if (
            reason.Length >
            300
        )
        {
            TempData["BarredError"] =
                "Reason cannot exceed 300 characters.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        barredStudent.Reason =
            reason;

        await _context
            .SaveChangesAsync();

        TempData["BarredSuccess"] =
            "Barred record updated successfully.";

        return RedirectToAction(
            nameof(BarredList)
        );
    }

    // ==================================================
    // REMOVE FROM BARRED LIST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBarredStudent(
        string id)
    {
        BarredStudent? barredStudent =
            await _context
                .BarredStudents
                .FirstOrDefaultAsync(b =>
                    b.BarredStudentId ==
                    id
                );

        if (
            barredStudent ==
            null
        )
        {
            TempData["BarredError"] =
                "Barred record could not be found.";

            return RedirectToAction(
                nameof(BarredList)
            );
        }

        _context
            .BarredStudents
            .Remove(
                barredStudent
            );

        await _context
            .SaveChangesAsync();

        TempData["BarredSuccess"] =
            "Student removed from barred list successfully.";

        return RedirectToAction(
            nameof(BarredList)
        );
    }

    // ==================================================
    // CALCULATE ATTENDANCE FOR BARRED LIST
    // ==================================================
    // IMPORTANT:
    // This now uses the SAME occurrence/status rules as
    // Student Attendance and Attendance Overview.
    //
    // Therefore:
    // - No Generate Code + class already ended = Absent
    // - Approved Leave = Leave and is excluded from formula
    // - Removed / Absent record = Absent
    // - Timetable cancellation is excluded
    // - Changed class time uses the override time
    // - Replacement class uses replacement date/time
    // ==================================================

    private async Task<AttendanceCalculationResult>
        CalculateAttendanceAsync(
            StudentClass studentClass,
            string courseId)
    {
        DateTime now =
            DateTime.Now;

        DateTime today =
            DateTime.Today;


        // ==============================================
        // ACTIVE TERM
        // ==============================================

        AcademicTerm? activeTerm =
            await _context
                .AcademicTerms
                .AsNoTracking()
                .Where(t =>
                    t.Status == "Active"
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
            return new AttendanceCalculationResult
            {
                AbsentCount = 0,
                Percentage = 100
            };
        }


        // ==============================================
        // COURSE TIMETABLE FOR THIS STUDENT'S CLASS
        // ==============================================

        List<Timetable> timetables =
            await _context
                .Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId ==
                        studentClass.ClassId &&
                    t.CourseId ==
                        courseId &&
                    t.TermId ==
                        activeTerm.TermId
                )
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
            return new AttendanceCalculationResult
            {
                AbsentCount = 0,
                Percentage = 100
            };
        }


        List<string> timetableIds =
            timetables
                .Select(t =>
                    t.TimetableId
                )
                .Distinct()
                .ToList();


        DateTime fromDate =
            studentClass.RegisteredDate.Date >
            activeTerm.StartDate.Date
                ? studentClass.RegisteredDate.Date
                : activeTerm.StartDate.Date;


        DateTime toDate =
            today <
            activeTerm.EndDate.Date
                ? today
                : activeTerm.EndDate.Date;


        if (
            fromDate >
            toDate
        )
        {
            return new AttendanceCalculationResult
            {
                AbsentCount = 0,
                Percentage = 100
            };
        }


        // ==============================================
        // TIMETABLE EXCEPTIONS
        // ==============================================

        List<TimetableException> exceptions =
            await _context
                .TimetableExceptions
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


        // ==============================================
        // REPLACEMENT CLASSES
        // ==============================================

        List<ReplacementSession> replacements =
            await _context
                .ReplacementSessions
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


        // ==============================================
        // BUILD THE CLASSES THAT SHOULD HAVE HAPPENED
        // ==============================================

        List<AdminAttendanceOccurrence> occurrences =
            BuildAdminAttendanceOccurrences(
                studentClass,
                timetables,
                exceptions,
                replacements,
                activeTerm,
                today
            );


        // Only completed classes affect percentage.
        List<AdminAttendanceOccurrence> endedOccurrences =
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


        if (
            endedOccurrences.Count ==
            0
        )
        {
            return new AttendanceCalculationResult
            {
                AbsentCount = 0,
                Percentage = 100
            };
        }


        // ==============================================
        // ATTENDANCE SESSIONS
        // ==============================================

        DateTime firstOccurrenceDate =
            endedOccurrences
                .Min(o =>
                    o.Date
                );


        List<AttendanceSession> sessions =
            await _context
                .AttendanceSessions
                .AsNoTracking()
                .Where(s =>
                    timetableIds.Contains(
                        s.TimetableId
                    ) &&
                    s.SessionDate.Date >=
                        firstOccurrenceDate &&
                    s.SessionDate.Date <=
                        toDate
                )
                .ToListAsync();


        Dictionary<string, AttendanceSession>
            sessionMap =
                sessions
                    .GroupBy(s =>
                        GetAdminAttendanceOccurrenceKey(
                            s.TimetableId,
                            s.SessionDate
                        )
                    )
                    .ToDictionary(
                        g =>
                            g.Key,
                        g =>
                            g.OrderByDescending(s =>
                                s.GeneratedAt
                            )
                            .First()
                    );


        // ==============================================
        // ATTENDANCE RECORDS
        // ==============================================

        List<string> sessionIds =
            sessions
                .Select(s =>
                    s.AttendanceSessionId
                )
                .Distinct()
                .ToList();


        List<AttendanceRecord> records =
            sessionIds.Count ==
            0
                ? new List<AttendanceRecord>()
                : await _context
                    .AttendanceRecords
                    .AsNoTracking()
                    .Where(r =>
                        sessionIds.Contains(
                            r.AttendanceSessionId
                        ) &&
                        r.StudentClassId ==
                            studentClass.StudentClassId
                    )
                    .ToListAsync();


        Dictionary<string, AttendanceRecord>
            recordMap =
                records
                    .GroupBy(r =>
                        r.AttendanceSessionId
                    )
                    .ToDictionary(
                        g =>
                            g.Key,
                        g =>
                            g.OrderByDescending(r =>
                                r.SubmittedAt
                            )
                            .First()
                    );


        // ==============================================
        // APPROVED LEAVE
        // ==============================================

        List<LeaveApplication> approvedLeaves =
            await _context
                .LeaveApplications
                .AsNoTracking()
                .Where(l =>
                    l.StudentId ==
                        studentClass.UserId &&
                    l.Status ==
                        "Approved" &&
                    l.EndDate.Date >=
                        firstOccurrenceDate &&
                    l.StartDate.Date <=
                        toDate
                )
                .ToListAsync();


        // ==============================================
        // CALCULATE PRESENT / ABSENT / LEAVE
        // ==============================================

        int present =
            0;

        int absent =
            0;


        foreach (
            AdminAttendanceOccurrence occurrence
            in endedOccurrences
        )
        {
            string occurrenceKey =
                GetAdminAttendanceOccurrenceKey(
                    occurrence.TimetableId,
                    occurrence.Date
                );


            sessionMap.TryGetValue(
                occurrenceKey,
                out AttendanceSession? session
            );


            AttendanceRecord? record =
                null;


            if (
                session !=
                null
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
                ResolveAdminAttendanceStatus(
                    occurrence,
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
                "Absent"
            )
            {
                absent++;
            }

            // Leave is intentionally excluded from the formula.
        }


        // ==============================================
        // SAME FORMULA AS STUDENT ATTENDANCE
        //
        // Attendance % =
        // (Present + 4) / (Present + Absent + 4) × 100
        //
        // Approved Leave is excluded.
        // ==============================================

        const int AttendanceStartingBuffer =
            4;


        int countedClasses =
            present +
            absent;


        double percentage =
            countedClasses ==
            0
                ? 100
                : Math.Round(
                    (
                        present +
                        AttendanceStartingBuffer
                    )
                    *
                    100.0
                    /
                    (
                        present +
                        absent +
                        AttendanceStartingBuffer
                    ),
                    1
                );


        return new AttendanceCalculationResult
        {
            AbsentCount =
                absent,

            Percentage =
                percentage
        };
    }

    // ==================================================
    // ATTENDANCE CALCULATION RESULT
    // ==================================================

    private class AttendanceCalculationResult
    {
        public int AbsentCount { get; set; }

        public double Percentage { get; set; }
    }

    // ==================================================
    // TIMETABLE PAGE
    // ==================================================

    public IActionResult Timetable()
    {
        return View();
    }

    // ==================================================
    // GET TEACHERS
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers =
            await _context.Users
                .Where(u =>
                    u.Position ==
                    "Teacher"
                )
                .OrderBy(
                    u => u.Username)
                .Select(u =>
                    new TeacherLookupDto
                    {
                        TeacherId =
                            u.UserId,

                        TeacherName =
                            u.Username
                    })
                .ToListAsync();

        return Json(teachers);
    }

    // ==================================================
    // GET COURSES
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> GetCourses()
    {
        var courses =
            await _context.Courses
                .OrderBy(
                    c => c.CourseName)
                .Select(c =>
                    new SimpleLookupDto
                    {
                        Id =
                            c.CourseId,

                        Name =
                            c.CourseName
                    })
                .ToListAsync();

        return Json(courses);
    }

    // ==================================================
    // GET TEACHER COURSES
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> GetTeacherCourses(
        string teacherId)
    {
        if (string.IsNullOrWhiteSpace(
            teacherId))
        {
            return BadRequest(
                "Teacher ID is required."
            );
        }

        var courses =
            await _context
                .TeacherCourses
                .AsNoTracking()
                .Where(tc =>
                    tc.UserId ==
                    teacherId
                )
                .Include(
                    tc => tc.Course)
                .OrderBy(tc =>
                    tc.Course!
                        .CourseName
                )
                .Select(tc =>
                    new SimpleLookupDto
                    {
                        Id =
                            tc.CourseId,

                        Name =
                            tc.Course!
                                .CourseName
                    })
                .ToListAsync();

        return Json(courses);
    }

    // ==================================================
    // GET SUBJECTS FOR TEACHER
    // ==================================================

    [HttpGet]
    public async Task<IActionResult>
        GetTeacherSubjectRegistration(
            string teacherId)
    {
        if (string.IsNullOrWhiteSpace(teacherId))
        {
            return BadRequest("Teacher ID is required.");
        }

        User? teacher =
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.UserId == teacherId &&
                    u.Position == "Teacher" &&
                    !u.IsDeleted
                );

        if (teacher == null)
        {
            return NotFound("Teacher not found.");
        }

        List<string> selectedProgramIds =
            await _context.TeacherPrograms
                .AsNoTracking()
                .Where(tp => tp.UserId == teacherId)
                .Select(tp => tp.ProgramId)
                .ToListAsync();

        List<string> selectedCourseIds =
            await _context.TeacherCourses
                .AsNoTracking()
                .Where(tc => tc.UserId == teacherId)
                .Select(tc => tc.CourseId)
                .ToListAsync();

        var programs =
            await _context.AcademicPrograms
                .AsNoTracking()
                .OrderBy(p => p.ProgramName)
                .Select(p => new
                {
                    programId = p.ProgramId,
                    programCode = p.ProgramCode,
                    programName = p.ProgramName,
                    selected = selectedProgramIds.Contains(p.ProgramId)
                })
                .ToListAsync();

        var programSubjects =
            await _context.ProgramCourses
                .AsNoTracking()
                .Include(pc => pc.Course)
                .Include(pc => pc.Program)
                .Where(pc => pc.Course != null)
                .Select(pc => new
                {
                    pc.CourseId,
                    CourseCode = pc.Course!.CourseCode,
                    CourseName = pc.Course.CourseName,
                    pc.ProgramId,
                    ProgramName = pc.Program != null
                        ? pc.Program.ProgramName
                        : pc.ProgramId
                })
                .ToListAsync();

        var subjects =
            programSubjects
                .GroupBy(s => new
                {
                    s.CourseId,
                    s.CourseCode,
                    s.CourseName
                })
                .Select(g => new
                {
                    courseId = g.Key.CourseId,
                    courseCode = g.Key.CourseCode ?? "",
                    courseName = g.Key.CourseName,
                    programIds = g
                        .Select(x => x.ProgramId)
                        .Distinct()
                        .ToList(),
                    programs = string.Join(
                        ", ",
                        g.Select(x => x.ProgramName)
                            .Distinct()
                    ),
                    selected = selectedCourseIds.Contains(g.Key.CourseId)
                })
                .OrderBy(s => s.courseName)
                .ToList();

        return Json(new
        {
            teacherId = teacher.UserId,
            teacherName = teacher.Username,
            hasProgram = selectedProgramIds.Count > 0,
            programs,
            subjects
        });
    }

    // ==================================================
    // SAVE PROGRAMS + SUBJECTS FOR TEACHER
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTeacherSubjects(
        string teacherId,
        List<string>? selectedProgramIds,
        List<string>? selectedCourseIds)
    {
        selectedProgramIds ??= new List<string>();
        selectedCourseIds ??= new List<string>();

        User? teacher =
            await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == teacherId &&
                    u.Position == "Teacher" &&
                    !u.IsDeleted
                );

        if (teacher == null)
        {
            TempData["ErrorMessage"] = "Teacher not found.";
            return RedirectToAction("Index", "User");
        }

        selectedProgramIds = selectedProgramIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        selectedCourseIds = selectedCourseIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (selectedProgramIds.Count == 0)
        {
            TempData["ErrorMessage"] =
                "Please select at least one program for this teacher.";
            return RedirectToAction("Index", "User");
        }

        List<string> validProgramIds =
            await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => selectedProgramIds.Contains(p.ProgramId))
                .Select(p => p.ProgramId)
                .ToListAsync();

        if (validProgramIds.Count != selectedProgramIds.Count)
        {
            TempData["ErrorMessage"] =
                "One or more selected programs are invalid.";
            return RedirectToAction("Index", "User");
        }

        List<string> allowedCourseIds =
            await _context.ProgramCourses
                .AsNoTracking()
                .Where(pc => selectedProgramIds.Contains(pc.ProgramId))
                .Select(pc => pc.CourseId)
                .Distinct()
                .ToListAsync();

        // Never trust course IDs posted by the browser.
        selectedCourseIds = selectedCourseIds
            .Where(id => allowedCourseIds.Contains(id))
            .ToList();

        List<TeacherCourse> currentCourses =
            await _context.TeacherCourses
                .Where(tc => tc.UserId == teacherId)
                .ToListAsync();

        List<TeacherCourse> coursesToRemove =
            currentCourses
                .Where(tc => !selectedCourseIds.Contains(tc.CourseId))
                .ToList();

        // A timetable assignment is historical/operational data.
        // Do not allow registration editing to make it invalid.
        foreach (TeacherCourse item in coursesToRemove)
        {
            bool usedInTimetable =
                await _context.Timetables
                    .AnyAsync(t =>
                        t.TeacherId == teacherId &&
                        t.CourseId == item.CourseId
                    );

            if (usedInTimetable)
            {
                Course? course =
                    await _context.Courses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.CourseId == item.CourseId);

                TempData["ErrorMessage"] =
                    $"Cannot remove {course?.CourseName ?? item.CourseId} " +
                    "because this teacher is already assigned to this subject in the timetable.";

                return RedirectToAction("Index", "User");
            }
        }

        List<TeacherProgram> currentPrograms =
            await _context.TeacherPrograms
                .Where(tp => tp.UserId == teacherId)
                .ToListAsync();

        List<TeacherProgram> programsToRemove =
            currentPrograms
                .Where(tp => !selectedProgramIds.Contains(tp.ProgramId))
                .ToList();

        _context.TeacherPrograms.RemoveRange(programsToRemove);

        List<string> existingProgramIds =
            currentPrograms
                .Select(tp => tp.ProgramId)
                .ToList();

        foreach (string programId in selectedProgramIds)
        {
            if (existingProgramIds.Contains(programId))
            {
                continue;
            }

            _context.TeacherPrograms.Add(new TeacherProgram
            {
                UserId = teacherId,
                ProgramId = programId,
                RegisteredDate = DateTime.Now
            });
        }

        _context.TeacherCourses.RemoveRange(coursesToRemove);

        List<string> existingCourseIds =
            currentCourses
                .Select(tc => tc.CourseId)
                .ToList();

        foreach (string courseId in selectedCourseIds)
        {
            if (existingCourseIds.Contains(courseId))
            {
                continue;
            }

            _context.TeacherCourses.Add(new TeacherCourse
            {
                TeacherCourseId =
                    await FormattedIdGenerator.NextAsync(
                        _context.TeacherCourses,
                        x => x.TeacherCourseId,
                        "TCR"
                    ),
                UserId = teacherId,
                CourseId = courseId,
                RegisteredDate = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"Programs and subjects for {teacher.Username} updated successfully.";

        return RedirectToAction("Index", "User");
    }

    // ==================================================
    // CREATE COURSE
    // ==================================================

    [HttpPost]
    public async Task<IActionResult> CreateCourse(
        [FromBody] SimpleLookupDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Name))
        {
            return BadRequest(
                "Course name is required."
            );
        }

        string courseName =
            dto.Name.Trim();

        bool alreadyExists =
            await _context
                .Courses
                .AnyAsync(c =>
                    c.CourseName ==
                    courseName
                );

        if (alreadyExists)
        {
            return BadRequest(
                "This course already exists."
            );
        }

        Course course = new()
        {
            CourseId =
                await FormattedIdGenerator
                    .NextAsync(
                        _context.Courses,
                        c => c.CourseId,
                        "CRS"
                    ),

            CourseName =
                courseName
        };

        _context
            .Courses
            .Add(course);

        await _context
            .SaveChangesAsync();

        return Json(
            new SimpleLookupDto
            {
                Id =
                    course.CourseId,

                Name =
                    course.CourseName
            }
        );
    }

    // ==================================================
    // DELETE COURSE
    // ==================================================

    [HttpPost]
    public async Task<IActionResult> DeleteCourse(
        string id)
    {
        Course? course =
            await _context
                .Courses
                .FindAsync(id);

        if (course == null)
        {
            return NotFound(
                "Course not found."
            );
        }

        bool inUse =
            await _context
                .Timetables
                .AnyAsync(t =>
                    t.CourseId ==
                    id
                );

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this course because " +
                "it is used in the timetable."
            );
        }

        _context
            .Courses
            .Remove(course);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Course deleted."
        });
    }

    // ==================================================
    // GET CLASSES
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> GetClasses()
    {
        var classes =
            await _context
                .ClassGroups
                .OrderBy(
                    c => c.ClassName)
                .Select(c =>
                    new SimpleLookupDto
                    {
                        Id =
                            c.ClassId,

                        Name =
                            c.ClassName
                    })
                .ToListAsync();

        return Json(classes);
    }

    // ==================================================
    // CREATE CLASS
    // ==================================================

    [HttpPost]
    public async Task<IActionResult> CreateClass(
        [FromBody] SimpleLookupDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Name))
        {
            return BadRequest(
                "Class name is required."
            );
        }

        string className =
            dto.Name.Trim();

        bool alreadyExists =
            await _context
                .ClassGroups
                .AnyAsync(c =>
                    c.ClassName ==
                    className
                );

        if (alreadyExists)
        {
            return BadRequest(
                "This class already exists."
            );
        }

        ClassGroup classGroup =
            new()
            {
                ClassId =
                    await FormattedIdGenerator
                        .NextAsync(
                            _context.ClassGroups,
                            c => c.ClassId,
                            "CLS"
                        ),

                ClassName =
                    className
            };

        _context
            .ClassGroups
            .Add(classGroup);

        await _context
            .SaveChangesAsync();

        return Json(
            new SimpleLookupDto
            {
                Id =
                    classGroup.ClassId,

                Name =
                    classGroup.ClassName
            }
        );
    }

    // ==================================================
    // DELETE CLASS
    // ==================================================

    [HttpPost]
    public async Task<IActionResult> DeleteClass(
        string id)
    {
        ClassGroup? classGroup =
            await _context
                .ClassGroups
                .FindAsync(id);

        if (classGroup == null)
        {
            return NotFound(
                "Class not found."
            );
        }

        bool inUse =
            await _context
                .Timetables
                .AnyAsync(t =>
                    t.ClassId ==
                    id
                );

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this class because " +
                "it is used in the timetable."
            );
        }

        _context
            .ClassGroups
            .Remove(classGroup);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Class deleted."
        });
    }

    // ==================================================
    // GET CLASSROOMS
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> GetClassrooms()
    {
        var rooms =
            await _context
                .Classrooms
                .OrderBy(
                    c => c.RoomName)
                .Select(c =>
                    new SimpleLookupDto
                    {
                        Id =
                            c.ClassroomId,

                        Name =
                            c.RoomName
                    })
                .ToListAsync();

        return Json(rooms);
    }

    // ==================================================
    // CREATE CLASSROOM
    // ==================================================

    [HttpPost]
    public async Task<IActionResult> CreateClassroom(
        [FromBody] SimpleLookupDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Name))
        {
            return BadRequest(
                "Classroom name is required."
            );
        }

        string roomName =
            dto.Name.Trim();

        bool alreadyExists =
            await _context
                .Classrooms
                .AnyAsync(c =>
                    c.RoomName ==
                    roomName
                );

        if (alreadyExists)
        {
            return BadRequest(
                "This classroom already exists."
            );
        }

        Classroom room =
            new()
            {
                ClassroomId =
                    await FormattedIdGenerator
                        .NextAsync(
                            _context.Classrooms,
                            c => c.ClassroomId,
                            "ROM"
                        ),

                RoomName =
                    roomName
            };

        _context
            .Classrooms
            .Add(room);

        await _context
            .SaveChangesAsync();

        return Json(
            new SimpleLookupDto
            {
                Id =
                    room.ClassroomId,

                Name =
                    room.RoomName
            }
        );
    }

    // ==================================================
    // DELETE CLASSROOM
    // ==================================================

    [HttpPost]
    public async Task<IActionResult> DeleteClassroom(
        string id)
    {
        Classroom? room =
            await _context
                .Classrooms
                .FindAsync(id);

        if (room == null)
        {
            return NotFound(
                "Classroom not found."
            );
        }

        bool inUse =
            await _context
                .Timetables
                .AnyAsync(t =>
                    t.ClassroomId ==
                    id
                );

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this classroom because " +
                "it is used in the timetable."
            );
        }

        _context
            .Classrooms
            .Remove(room);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Classroom deleted."
        });
    }

    // ==================================================
    // GET TIMETABLE GRID
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> GetTimetableGrid(
        string classType)
    {
        if (!Enum.TryParse(
                classType,
                true,
                out ClassType parsedType))
        {
            return BadRequest(
                "Invalid class type. " +
                "Use School or Tuition."
            );
        }

        List<Timetable> entries =
            await _context
                .Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassType ==
                    parsedType
                )
                .Include(t =>
                    t.Course)
                .Include(t =>
                    t.ClassGroup)
                .Include(t =>
                    t.Classroom)
                .OrderBy(t =>
                    t.DayOfWeek)
                .ThenBy(t =>
                    t.StartTime)
                .ToListAsync();

        List<string> teacherIds =
            entries
                .Select(t =>
                    t.TeacherId)
                .Distinct()
                .ToList();

        Dictionary<string, string>
            teacherNames =
                await _context
                    .Users
                    .Where(u =>
                        teacherIds.Contains(
                            u.UserId
                        )
                    )
                    .ToDictionaryAsync(
                        u => u.UserId,
                        u => u.Username
                    );

        var result =
            entries.Select(t =>
                new TimetableSlotDto
                {
                    TimetableId =
                        t.TimetableId,

                    TeacherId =
                        t.TeacherId,

                    TeacherName =
                        teacherNames
                            .GetValueOrDefault(
                                t.TeacherId,
                                "Unknown"
                            ),

                    CourseId =
                        t.CourseId,

                    CourseName =
                        t.Course
                            ?.CourseName
                        ?? string.Empty,

                    ClassId =
                        t.ClassId,

                    ClassName =
                        t.ClassGroup
                            ?.ClassName
                        ?? string.Empty,

                    ClassroomId =
                        t.ClassroomId,

                    RoomName =
                        t.Classroom
                            ?.RoomName,

                    DayOfWeek =
                        t.DayOfWeek,

                    StartTime =
                        t.StartTime
                            .ToString(
                                @"hh\:mm"
                            ),

                    EndTime =
                        t.EndTime
                            .ToString(
                                @"hh\:mm"
                            ),

                    ClassType =
                        t.ClassType
                });

        return Json(result);
    }

    // ==================================================
    // CREATE TIMETABLE ENTRY
    // ==================================================

    [HttpPost]
    public async Task<IActionResult>
        CreateTimetableEntry(
            [FromBody]
            TimetableRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                GetModelErrors()
            );
        }

        // Teacher must be registered
        // to teach this subject
        bool teacherCanTeachCourse =
            await _context
                .TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId ==
                        request.TeacherId &&
                    tc.CourseId ==
                        request.CourseId
                );

        if (!teacherCanTeachCourse)
        {
            return BadRequest(
                "This teacher is not registered to teach this course."
            );
        }

        var validation =
            await _validationService
                .ValidateAsync(
                    request
                );

        if (!validation.IsValid)
        {
            return BadRequest(
                validation.ErrorMessage
            );
        }

        Timetable entry =
            new()
            {
                TimetableId =
                    await FormattedIdGenerator
                        .NextAsync(
                            _context.Timetables,
                            t => t.TimetableId,
                            "TTB"
                        ),

                TeacherId =
                    request.TeacherId,

                CourseId =
                    request.CourseId,

                ClassId =
                    request.ClassId,

                ClassroomId =
                    request.ClassroomId,

                DayOfWeek =
                    request.DayOfWeek,

                StartTime =
                    TimeSpan.Parse(
                        request.StartTime
                    ),

                EndTime =
                    TimeSpan.Parse(
                        request.EndTime
                    ),

                ClassType =
                    request.ClassType
            };

        _context
            .Timetables
            .Add(entry);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry created.",

            timetableId =
                entry.TimetableId
        });
    }

    // ==================================================
    // UPDATE TIMETABLE ENTRY
    // ==================================================

    [HttpPost]
    public async Task<IActionResult>
        UpdateTimetableEntry(
            [FromBody]
            TimetableRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.TimetableId))
        {
            return BadRequest(
                "TimetableId is required for updates."
            );
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(
                GetModelErrors()
            );
        }

        bool teacherCanTeachCourse =
            await _context
                .TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId ==
                        request.TeacherId &&
                    tc.CourseId ==
                        request.CourseId
                );

        if (!teacherCanTeachCourse)
        {
            return BadRequest(
                "This teacher is not registered to teach this course."
            );
        }

        Timetable? entry =
            await _context
                .Timetables
                .FindAsync(
                    request.TimetableId
                );

        if (entry == null)
        {
            return NotFound(
                "Timetable entry not found."
            );
        }

        var validation =
            await _validationService
                .ValidateAsync(
                    request
                );

        if (!validation.IsValid)
        {
            return BadRequest(
                validation.ErrorMessage
            );
        }

        entry.TeacherId =
            request.TeacherId;

        entry.CourseId =
            request.CourseId;

        entry.ClassId =
            request.ClassId;

        entry.ClassroomId =
            request.ClassroomId;

        entry.DayOfWeek =
            request.DayOfWeek;

        entry.StartTime =
            TimeSpan.Parse(
                request.StartTime
            );

        entry.EndTime =
            TimeSpan.Parse(
                request.EndTime
            );

        entry.ClassType =
            request.ClassType;

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry updated."
        });
    }

    // ==================================================
    // DELETE TIMETABLE ENTRY
    // ==================================================

    [HttpPost]
    public async Task<IActionResult>
        DeleteTimetableEntry(
            string id)
    {
        Timetable? entry =
            await _context
                .Timetables
                .FirstOrDefaultAsync(t =>
                    t.TimetableId == id
                );

        if (entry == null)
        {
            return NotFound(
                "Timetable entry not found."
            );
        }


        // Get the subject name before deleting the timetable.
        string courseName =
            await _context
                .Courses
                .AsNoTracking()
                .Where(c =>
                    c.CourseId ==
                        entry.CourseId
                )
                .Select(c =>
                    c.CourseName
                )
                .FirstOrDefaultAsync()
            ?? entry.CourseId;


        // Create a NEW latest notification first.
        // The old LessonCancelled notification remains in history,
        // but Teacher Recent Timetable Changes will no longer use it
        // as the latest state after the timetable is deleted.
        await CreateTimetableNotificationAsync(
            entry,
            "Deleted",
            $"{courseName} has been removed from the timetable."
        );


        // Delete the timetable entry.
        _context
            .Timetables
            .Remove(entry);

        await _context
            .SaveChangesAsync();


        return Ok(new
        {
            message =
                "Timetable entry deleted."
        });
    }

    // ==================================================
    // CREATE TIMETABLE NOTIFICATION
    // ==================================================

    private async Task CreateTimetableNotificationAsync(
        Timetable timetable,
        string type,
        string message,
        DateTime? occurrenceDate = null)
    {
        string? termId =
            timetable.TermId;

        // Old timetable records may not have TermId.
        // If a specific lesson date is available,
        // try to determine the term from that date.
        if (string.IsNullOrWhiteSpace(termId))
        {
            if (occurrenceDate.HasValue)
            {
                DateTime date =
                    occurrenceDate.Value.Date;

                termId =
                    await _context
                        .AcademicTerms
                        .AsNoTracking()
                        .Where(t =>
                            date >= t.StartDate.Date &&
                            date <= t.EndDate.Date
                        )
                        .OrderBy(t =>
                            t.StartDate
                        )
                        .Select(t =>
                            t.TermId
                        )
                        .FirstOrDefaultAsync();
            }

            // If the date does not resolve a term,
            // use Active first, then Incoming.
            if (string.IsNullOrWhiteSpace(termId))
            {
                termId =
                    await _context
                        .AcademicTerms
                        .AsNoTracking()
                        .Where(t =>
                            t.Status == "Active" ||
                            t.Status == "Incoming"
                        )
                        .OrderBy(t =>
                            t.Status == "Active"
                                ? 0
                                : 1
                        )
                        .ThenBy(t =>
                            t.StartDate
                        )
                        .Select(t =>
                            t.TermId
                        )
                        .FirstOrDefaultAsync();
            }
        }

        // Get every student currently registered
        // in the class affected by this timetable.
        List<string> receiverIds =
            await _context
                .StudentClasses
                .AsNoTracking()
                .Where(sc =>
                    sc.ClassId == timetable.ClassId
                )
                .Select(sc =>
                    sc.UserId
                )
                .ToListAsync();

        // Teacher also receives the notification.
        if (!string.IsNullOrWhiteSpace(timetable.TeacherId))
        {
            receiverIds.Add(
                timetable.TeacherId
            );
        }

        // Remove empty and duplicate receiver IDs.
        receiverIds =
            receiverIds
                .Where(id =>
                    !string.IsNullOrWhiteSpace(id)
                )
                .Distinct()
                .ToList();

        foreach (string userId in receiverIds)
        {
            TimetableNotification notification =
                new()
                {
                    NotificationId =
                        await FormattedIdGenerator
                            .NextAsync(
                                _context.TimetableNotifications,
                                n => n.NotificationId,
                                "NTF"
                            ),

                    Type =
                        type,

                    Message =
                        message.Length > 250
                            ? message[..250]
                            : message,

                    TermId =
                        termId,

                    TimetableId =
                        timetable.TimetableId,

                    UserId =
                        userId,

                    OccurrenceDate =
                        occurrenceDate?.Date,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.Now
                };

            _context
                .TimetableNotifications
                .Add(notification);

            // Save each notification so that the next call
            // to FormattedIdGenerator sees the previous ID.
            await _context
                .SaveChangesAsync();
        }
    }

    // ==================================================
    // MODEL ERRORS
    // ==================================================

    private Dictionary<string, string>
        GetModelErrors()
    {
        return ModelState
            .Where(item =>
                item.Value
                    ?.Errors
                    .Count > 0
            )
            .ToDictionary(
                item =>
                    item.Key,

                item =>
                    item.Value!
                        .Errors
                        .First()
                        .ErrorMessage
            );
    }

    // ==================================================
    // CURRENT USER
    // ==================================================

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    // ==================================================
    // GET NON-TEACHER USERS
    // ==================================================

    [HttpGet]
    public async Task<IActionResult>
        GetNonTeacherUsers()
    {
        var users =
            await _context
                .Users
                .Where(u =>
                    u.Position ==
                        "Student" &&
                    !u.IsDeleted
                )
                .OrderBy(
                    u => u.Username)
                .Select(u =>
                    new TeacherLookupDto
                    {
                        TeacherId =
                            u.UserId,

                        TeacherName =
                            u.Username
                    })
                .ToListAsync();

        return Json(users);
    }

    // ==================================================
    // CREATE TEACHER PAGE
    // ==================================================

    [HttpGet]
    public IActionResult Createteacher()
    {
        return View();
    }

    // ==================================================
    // PROMOTE TO TEACHER
    // ==================================================

    [HttpPost]
    public async Task<IActionResult>
        PromoteToTeacher(
            string id)
    {
        var user =
            await _context
                .Users
                .FindAsync(id);

        if (
            user == null ||
            user.IsDeleted
        )
        {
            return NotFound(
                "User not found."
            );
        }

        if (
            user.Position ==
            "Teacher"
        )
        {
            return BadRequest(
                "This user is already a teacher."
            );
        }

        if (
            user.Position ==
            "Admin"
        )
        {
            return BadRequest(
                "Admin cannot be promoted to Teacher."
            );
        }

        user.Position =
            "Teacher";

        await _context
            .SaveChangesAsync();

        return Ok();
    }

    // ==================================================
    // GET ALL USERS WITH ROLES
    // ==================================================

    [HttpGet]
    public async Task<IActionResult>
        GetAllUsersWithRoles()
    {
        var users =
            await _context
                .Users
                .Where(u =>
                    !u.IsDeleted
                )
                .OrderBy(
                    u => u.Username)
                .Select(u =>
                    new UserRoleDto
                    {
                        UserId =
                            u.UserId,

                        Username =
                            u.Username,

                        Position =
                            u.Position
                    })
                .ToListAsync();

        return Json(users);
    }

    // ==================================================
    // CANCEL TEACHER ROLE
    // ==================================================

    [HttpPost]
    public async Task<IActionResult>
        CancelTeacherRole(
            string id)
    {
        var user =
            await _context
                .Users
                .FindAsync(id);

        if (
            user == null ||
            user.IsDeleted
        )
        {
            return NotFound(
                "User not found."
            );
        }

        if (
            user.Position ==
            "Admin"
        )
        {
            return BadRequest(
                "Cannot change an Admin's role."
            );
        }

        if (
            user.Position !=
            "Teacher"
        )
        {
            return BadRequest(
                "This user is not currently a teacher."
            );
        }

        user.Position =
            "Student";

        await _context
            .SaveChangesAsync();

        return Ok();
    }
}