using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

    public AdminController(
        DB context,
        ITimetableValidationService validationService)
    {
        _context = context;
        _validationService = validationService;
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

        ViewBag.PasskeyCount = passkeyCount;

        ViewBag.AdminPinEnabled =
            adminSecurity?.PinEnabled == true;

        return View(user);
    }

    // ==================================================
    // DASHBOARD - overall payment overview
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Dashboard(
        string? search,
        string? status)
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
            Search = search,

            StatusFilter =
                string.IsNullOrWhiteSpace(status)
                    ? "All"
                    : status,

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

        model.Payments =
            await filtered
                .OrderByDescending(
                    p => p.CreatedAt)
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

        return View(model);
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
    public async Task<IActionResult> BarredList()
    {
        BarredListViewModel model =
            new();

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
        // CURRENT BARRED RECORDS
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
        // BUILD CURRENT BARRED LIST
        // ==========================================

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

            model.BarredStudents.Add(
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
        // FIND STUDENTS WITH ATTENDANCE <= 80%
        // ==========================================

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

                model.EligibleStudents.Add(
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

        model.EligibleStudents =
            model.EligibleStudents
                .OrderBy(x =>
                    x.AttendancePercentage
                )
                .ThenBy(x =>
                    x.Username
                )
                .ToList();

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

    private async Task<AttendanceCalculationResult>
        CalculateAttendanceAsync(
            StudentClass studentClass,
            string courseId)
    {
        DateTime now =
            DateTime.Now;

        DateTime today =
            DateTime.Today;

        List<Timetable> timetables =
            await _context
                .Timetables
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

        if (
            timetableIds.Count ==
            0
        )
        {
            return new AttendanceCalculationResult
            {
                AbsentCount =
                    0,

                Percentage =
                    100
            };
        }

        List<AttendanceSession> sessions =
            await _context
                .AttendanceSessions
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

        int absent =
            0;

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

            // ==========================================
            // STUDENT REGISTRATION VALIDATION
            // ==========================================

            DateTime classStart =
                session.SessionDate.Date
                    .Add(
                        timetable.StartTime
                    );

            if (
                studentClass.RegisteredDate >
                classStart
            )
            {
                continue;
            }

            AttendanceRecord? record =
                await _context
                    .AttendanceRecords
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
                continue;
            }

            // ==========================================
            // APPROVED LEAVE
            // ==========================================

            bool approvedLeave =
                await _context
                    .LeaveApplications
                    .AsNoTracking()
                    .AnyAsync(l =>
                        l.StudentId ==
                            studentClass.UserId &&
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
                continue;
            }

            // ==========================================
            // CLASS END TIME
            // ==========================================

            DateTime classEnd =
                session.SessionDate.Date
                    .Add(
                        timetable.EndTime
                    );

            if (
                now <
                classEnd
            )
            {
                continue;
            }

            absent++;
        }

        // ==========================================
        // FORMULA
        // Attendance % = 100 - (Absent × 6.6667)
        // ==========================================

        const double AbsentPenalty =
            6.6667;

        double percentage =
            100 -
            (
                absent *
                AbsentPenalty
            );

        if (
            percentage <
            0
        )
        {
            percentage =
                0;
        }

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
        if (string.IsNullOrWhiteSpace(
            teacherId))
        {
            return BadRequest(
                "Teacher ID is required."
            );
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
            return NotFound(
                "Teacher not found."
            );
        }

        // Get programs registered by teacher
        List<string> programIds =
            await _context
                .TeacherPrograms
                .AsNoTracking()
                .Where(tp =>
                    tp.UserId ==
                    teacherId
                )
                .Select(tp =>
                    tp.ProgramId
                )
                .ToListAsync();

        if (programIds.Count == 0)
        {
            return Json(new
            {
                teacherId =
                    teacher.UserId,

                teacherName =
                    teacher.Username,

                hasProgram =
                    false,

                subjects =
                    Array.Empty<object>()
            });
        }

        // Get all subjects from teacher's programs
        var availableSubjects =
            await _context
                .ProgramCourses
                .AsNoTracking()
                .Where(pc =>
                    programIds.Contains(
                        pc.ProgramId
                    )
                )
                .Include(
                    pc => pc.Course)
                .Include(
                    pc => pc.Program)
                .Where(pc =>
                    pc.Course != null
                )
                .Select(pc =>
                    new
                    {
                        CourseId =
                            pc.CourseId,

                        CourseCode =
                            pc.Course!
                                .CourseCode,

                        CourseName =
                            pc.Course
                                .CourseName,

                        ProgramName =
                            pc.Program != null
                                ? pc.Program
                                    .ProgramName
                                : ""
                    })
                .ToListAsync();

        // Subjects already registered to teacher
        List<string> selectedCourseIds =
            await _context
                .TeacherCourses
                .AsNoTracking()
                .Where(tc =>
                    tc.UserId ==
                    teacherId
                )
                .Select(tc =>
                    tc.CourseId
                )
                .ToListAsync();

        var subjects =
            availableSubjects
                .GroupBy(s =>
                    new
                    {
                        s.CourseId,
                        s.CourseCode,
                        s.CourseName
                    })
                .Select(g =>
                    new
                    {
                        courseId =
                            g.Key.CourseId,

                        courseCode =
                            g.Key.CourseCode
                            ?? "",

                        courseName =
                            g.Key.CourseName,

                        programs =
                            string.Join(
                                ", ",
                                g.Select(
                                    x =>
                                        x.ProgramName
                                )
                                .Distinct()
                            ),

                        selected =
                            selectedCourseIds
                                .Contains(
                                    g.Key.CourseId
                                )
                    })
                .OrderBy(
                    s => s.courseName)
                .ToList();

        return Json(new
        {
            teacherId =
                teacher.UserId,

            teacherName =
                teacher.Username,

            hasProgram =
                true,

            subjects
        });
    }

    // ==================================================
    // SAVE SUBJECTS FOR TEACHER
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTeacherSubjects(
        string teacherId,
        List<string>? selectedCourseIds)
    {
        selectedCourseIds ??=
            new List<string>();

        // Find teacher
        User? teacher =
            await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == teacherId &&
                    u.Position == "Teacher" &&
                    !u.IsDeleted
                );

        if (teacher == null)
        {
            TempData["ErrorMessage"] =
                "Teacher not found.";

            return RedirectToAction(
                "Index",
                "User"
            );
        }

        // Get teacher's registered programs
        List<string> programIds =
            await _context
                .TeacherPrograms
                .AsNoTracking()
                .Where(tp =>
                    tp.UserId ==
                    teacherId
                )
                .Select(tp =>
                    tp.ProgramId
                )
                .ToListAsync();

        if (programIds.Count == 0)
        {
            TempData["ErrorMessage"] =
                "This teacher has not registered any program.";

            return RedirectToAction(
                "Index",
                "User"
            );
        }

        // Get all valid subjects for teacher's programs
        List<string> allowedCourseIds =
            await _context
                .ProgramCourses
                .AsNoTracking()
                .Where(pc =>
                    programIds.Contains(
                        pc.ProgramId
                    )
                )
                .Select(pc =>
                    pc.CourseId
                )
                .Distinct()
                .ToListAsync();

        // Only accept valid course IDs
        selectedCourseIds =
            selectedCourseIds
                .Where(id =>
                    allowedCourseIds.Contains(
                        id
                    )
                )
                .Distinct()
                .ToList();

        // Get current teacher subjects
        List<TeacherCourse> currentCourses =
            await _context
                .TeacherCourses
                .Where(tc =>
                    tc.UserId ==
                    teacherId
                )
                .ToListAsync();

        // Find subjects unchecked by admin
        List<TeacherCourse> coursesToRemove =
            currentCourses
                .Where(tc =>
                    !selectedCourseIds
                        .Contains(
                            tc.CourseId
                        )
                )
                .ToList();

        // Prevent removing a subject
        // already used in timetable
        foreach (
            TeacherCourse item
            in coursesToRemove)
        {
            bool usedInTimetable =
                await _context
                    .Timetables
                    .AnyAsync(t =>
                        t.TeacherId ==
                            teacherId &&
                        t.CourseId ==
                            item.CourseId
                    );

            if (usedInTimetable)
            {
                Course? course =
                    await _context
                        .Courses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c =>
                            c.CourseId ==
                                item.CourseId
                        );

                TempData["ErrorMessage"] =
                    $"Cannot remove " +
                    $"{course?.CourseName ?? item.CourseId} " +
                    "because this teacher is already assigned " +
                    "to this subject in the timetable.";

                return RedirectToAction(
                    "Index",
                    "User"
                );
            }
        }

        // Remove unchecked subjects
        _context
            .TeacherCourses
            .RemoveRange(
                coursesToRemove
            );

        // Current course IDs
        List<string> existingCourseIds =
            currentCourses
                .Select(tc =>
                    tc.CourseId
                )
                .ToList();

        // Add newly selected subjects
        foreach (
            string courseId
            in selectedCourseIds)
        {
            // Already registered
            if (
                existingCourseIds
                    .Contains(
                        courseId
                    )
            )
            {
                continue;
            }

            TeacherCourse teacherCourse =
                new TeacherCourse
                {
                    TeacherCourseId =
                        await FormattedIdGenerator
                            .NextAsync(
                                _context
                                    .TeacherCourses,

                                x =>
                                    x.TeacherCourseId,

                                "TCR"
                            ),

                    UserId =
                        teacherId,

                    CourseId =
                        courseId,

                    RegisteredDate =
                        DateTime.Now
                };

            _context
                .TeacherCourses
                .Add(
                    teacherCourse
                );
        }

        await _context
            .SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"Subjects for {teacher.Username} updated successfully.";

        return RedirectToAction(
            "Index",
            "User"
        );
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
                .FindAsync(id);

        if (entry == null)
        {
            return NotFound(
                "Timetable entry not found."
            );
        }

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