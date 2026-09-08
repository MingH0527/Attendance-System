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
            now.TimeOfDay >=
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


    // ==================================================
    // MANAGE ATTENDANCE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Manage(
        string timetableId,
        DateTime date,
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

        ManagedLessonContext? lesson =
            await ResolveManagedLessonAsync(
                userId,
                timetableId,
                date,
                replacementId
            );

        if (
            lesson ==
            null
        )
        {
            return NotFound();
        }

        DateTime classStart =
            lesson.Date.Add(
                lesson.StartTime
            );

        DateTime classEnd =
            lesson.Date.Add(
                lesson.EndTime
            );

        if (
            now <
            classStart
        )
        {
            TempData["AttendanceError"] =
                "Attendance can only be viewed after the class starts.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }

        AttendanceSession? session =
            await db.AttendanceSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.TimetableId ==
                        lesson.TimetableId &&
                    s.SessionDate ==
                        lesson.Date
                );

        bool hasGeneratedCode =
            session != null &&
            session.IsActive;

        // If the teacher did not generate a code,
        // View Students is only available after class ends.
        if (
            !hasGeneratedCode &&
            now <
            classEnd
        )
        {
            TempData["AttendanceError"] =
                "Attendance code has not been generated. " +
                "Manual attendance is only available after the class has ended.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }

        List<StudentClass> students =
            await db.StudentClasses
                .AsNoTracking()
                .Where(sc =>
                    sc.ClassId ==
                        lesson.ClassId &&
                    sc.User !=
                        null &&
                    sc.User.Position ==
                        "Student" &&
                    !sc.User.IsDeleted
                )
                .Include(sc =>
                    sc.User
                )
                .OrderBy(sc =>
                    sc.StudentId
                )
                .ToListAsync();

        students =
            students
                .Where(sc =>
                    sc.RegisteredDate <=
                        classStart
                )
                .ToList();

        List<AttendanceRecord> records =
            new();

        if (
            session !=
            null
        )
        {
            List<string> studentClassIds =
                students
                    .Select(sc =>
                        sc.StudentClassId
                    )
                    .ToList();

            records =
                await db.AttendanceRecords
                    .AsNoTracking()
                    .Where(r =>
                        r.AttendanceSessionId ==
                            session.AttendanceSessionId &&
                        studentClassIds.Contains(
                            r.StudentClassId
                        )
                    )
                    .ToListAsync();
        }

        List<string> studentUserIds =
            students
                .Select(sc =>
                    sc.UserId
                )
                .ToList();

        List<LeaveApplication> approvedLeaves =
            await db.LeaveApplications
                .AsNoTracking()
                .Where(l =>
                    studentUserIds.Contains(
                        l.StudentId
                    ) &&
                    l.Status ==
                        "Approved" &&
                    lesson.Date >=
                        l.StartDate.Date &&
                    lesson.Date <=
                        l.EndDate.Date
                )
                .ToListAsync();

        TeacherManageAttendanceViewModel model =
            new()
            {
                TimetableId =
                    lesson.TimetableId,

                ReplacementId =
                    lesson.ReplacementId,

                Date =
                    lesson.Date,

                CourseName =
                    lesson.CourseName,

                ClassName =
                    lesson.ClassName,

                RoomName =
                    lesson.RoomName,

                StartTime =
                    lesson.StartTime,

                EndTime =
                    lesson.EndTime,

                ClassEnded =
                    now >=
                    classEnd,

                HasGeneratedCode =
                    hasGeneratedCode
            };

        foreach (
            StudentClass student
            in students
        )
        {
            AttendanceRecord? record =
                records
                    .FirstOrDefault(r =>
                        r.StudentClassId ==
                            student.StudentClassId
                    );

            LeaveApplication? leave =
                approvedLeaves
                    .FirstOrDefault(l =>
                        l.StudentId ==
                            student.UserId
                    );

            string status;
            string? description =
                null;

            // ==================================================
            // LEAVE
            // ==================================================

            if (
                leave !=
                null
            )
            {
                status =
                    "Leave";

                description =
                    string.IsNullOrWhiteSpace(
                        leave.Reason
                    )
                        ? leave.LeaveType
                        : $"{leave.LeaveType}: {leave.Reason}";
            }

            // ==================================================
            // PRESENT
            // ==================================================

            else if (
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
                status =
                    "Present";

                description =
                    record.Description;
            }

            // ==================================================
            // ABSENT
            // ==================================================

            else
            {
                status =
                    "Absent";

                description =
                    record?.Description;
            }

            model.Students.Add(
                new TeacherManageAttendanceStudentViewModel
                {
                    StudentClassId =
                        student.StudentClassId,

                    UserId =
                        student.UserId,

                    StudentId =
                        student.StudentId,

                    Username =
                        student.User?.Username
                        ?? student.UserId,

                    Status =
                        status,

                    Description =
                        description,

                    CanEdit =
                        leave ==
                        null
                }
            );
        }

        return View(
            model
        );
    }


    // ==================================================
    // TEACHER MARK PRESENT
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPresent(
        string timetableId,
        DateTime date,
        string studentClassId,
        string? replacementId)
    {
        string? userId =
            GetCurrentUserId();

        if (
            userId ==
            null
        )
        {
            return Unauthorized();
        }

        ManagedLessonContext? lesson =
            await ResolveManagedLessonAsync(
                userId,
                timetableId,
                date,
                replacementId
            );

        if (
            lesson ==
            null
        )
        {
            return NotFound();
        }

        StudentClass? studentClass =
            await db.StudentClasses
                .FirstOrDefaultAsync(sc =>
                    sc.StudentClassId ==
                        studentClassId &&
                    sc.ClassId ==
                        lesson.ClassId
                );

        if (
            studentClass ==
            null
        )
        {
            return NotFound();
        }

        DateTime now =
            DateTime.Now;

        DateTime classStart =
            lesson.Date.Add(
                lesson.StartTime
            );

        DateTime classEnd =
            lesson.Date.Add(
                lesson.EndTime
            );

        if (
            now <
            classStart
        )
        {
            TempData["AttendanceError"] =
                "Attendance cannot be marked before the class starts.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }

        AttendanceSession? existingSession =
            await db.AttendanceSessions
                .FirstOrDefaultAsync(s =>
                    s.TimetableId ==
                        lesson.TimetableId &&
                    s.SessionDate ==
                        lesson.Date
                );

        bool hasGeneratedCode =
            existingSession != null &&
            existingSession.IsActive;

        if (
            !hasGeneratedCode &&
            now <
            classEnd
        )
        {
            TempData["AttendanceError"] =
                "Manual attendance is only available after the class has ended " +
                "when no attendance code was generated.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }

        bool approvedLeave =
            await db.LeaveApplications
                .AsNoTracking()
                .AnyAsync(l =>
                    l.StudentId ==
                        studentClass.UserId &&
                    l.Status ==
                        "Approved" &&
                    lesson.Date >=
                        l.StartDate.Date &&
                    lesson.Date <=
                        l.EndDate.Date
                );

        if (
            approvedLeave
        )
        {
            TempData["AttendanceError"] =
                "This student has approved leave for this date.";

            return RedirectToManage(
                lesson
            );
        }

        AttendanceSession session =
            existingSession
            ?? await GetOrCreateManualSessionAsync(
                lesson,
                now
            );

        AttendanceRecord? record =
            await db.AttendanceRecords
                .FirstOrDefaultAsync(r =>
                    r.AttendanceSessionId ==
                        session.AttendanceSessionId &&
                    r.StudentClassId ==
                        studentClass.StudentClassId
                );

        if (
            record ==
            null
        )
        {
            record =
                new AttendanceRecord
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
                        session.AttendanceSessionId,

                    StudentClassId =
                        studentClass.StudentClassId,

                    Status =
                        "Present",

                    SubmittedAt =
                        now,

                    IsRemoved =
                        false,

                    Description =
                        null,

                    RemovedAt =
                        null
                };

            db.AttendanceRecords.Add(
                record
            );
        }
        else
        {
            record.Status =
                "Present";

            record.SubmittedAt =
                now;

            record.IsRemoved =
                false;

            record.Description =
                null;

            record.RemovedAt =
                null;
        }

        await db.SaveChangesAsync();

        TempData["AttendanceSuccess"] =
            "Student marked as Present.";

        return RedirectToManage(
            lesson
        );
    }


    // ==================================================
    // TEACHER MARK ABSENT
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAbsent(
        string timetableId,
        DateTime date,
        string studentClassId,
        string? replacementId,
        string? reason)
    {
        string? userId =
            GetCurrentUserId();

        if (
            userId ==
            null
        )
        {
            return Unauthorized();
        }

        ManagedLessonContext? lesson =
            await ResolveManagedLessonAsync(
                userId,
                timetableId,
                date,
                replacementId
            );

        if (
            lesson ==
            null
        )
        {
            return NotFound();
        }

        StudentClass? studentClass =
            await db.StudentClasses
                .FirstOrDefaultAsync(sc =>
                    sc.StudentClassId ==
                        studentClassId &&
                    sc.ClassId ==
                        lesson.ClassId
                );

        if (
            studentClass ==
            null
        )
        {
            return NotFound();
        }

        DateTime now =
            DateTime.Now;

        DateTime classStart =
            lesson.Date.Add(
                lesson.StartTime
            );

        DateTime classEnd =
            lesson.Date.Add(
                lesson.EndTime
            );

        if (
            now <
            classStart
        )
        {
            TempData["AttendanceError"] =
                "Attendance cannot be marked before the class starts.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }

        AttendanceSession? existingSession =
            await db.AttendanceSessions
                .FirstOrDefaultAsync(s =>
                    s.TimetableId ==
                        lesson.TimetableId &&
                    s.SessionDate ==
                        lesson.Date
                );

        bool hasGeneratedCode =
            existingSession != null &&
            existingSession.IsActive;

        if (
            !hasGeneratedCode &&
            now <
            classEnd
        )
        {
            TempData["AttendanceError"] =
                "Manual attendance is only available after the class has ended " +
                "when no attendance code was generated.";

            return RedirectToAction(
                "AttendanceCode",
                "Teacher"
            );
        }

        bool approvedLeave =
            await db.LeaveApplications
                .AsNoTracking()
                .AnyAsync(l =>
                    l.StudentId ==
                        studentClass.UserId &&
                    l.Status ==
                        "Approved" &&
                    lesson.Date >=
                        l.StartDate.Date &&
                    lesson.Date <=
                        l.EndDate.Date
                );

        if (
            approvedLeave
        )
        {
            TempData["AttendanceError"] =
                "Approved leave cannot be changed to Absent.";

            return RedirectToManage(
                lesson
            );
        }

        reason =
            reason?.Trim();

        if (
            string.IsNullOrWhiteSpace(
                reason
            )
        )
        {
            TempData["AttendanceError"] =
                "Please enter a reason before marking the student as Absent.";

            return RedirectToManage(
                lesson
            );
        }

        if (
            reason.Length >
            500
        )
        {
            TempData["AttendanceError"] =
                "Reason cannot exceed 500 characters.";

            return RedirectToManage(
                lesson
            );
        }

        AttendanceSession session =
            existingSession
            ?? await GetOrCreateManualSessionAsync(
                lesson,
                now
            );

        AttendanceRecord? record =
            await db.AttendanceRecords
                .FirstOrDefaultAsync(r =>
                    r.AttendanceSessionId ==
                        session.AttendanceSessionId &&
                    r.StudentClassId ==
                        studentClass.StudentClassId
                );

        if (
            record ==
            null
        )
        {
            record =
                new AttendanceRecord
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
                        session.AttendanceSessionId,

                    StudentClassId =
                        studentClass.StudentClassId,

                    Status =
                        "Absent",

                    SubmittedAt =
                        now,

                    IsRemoved =
                        true,

                    Description =
                        reason,

                    RemovedAt =
                        now
                };

            db.AttendanceRecords.Add(
                record
            );
        }
        else
        {
            record.Status =
                "Absent";

            record.SubmittedAt =
                now;

            record.IsRemoved =
                true;

            record.Description =
                reason;

            record.RemovedAt =
                now;
        }

        await db.SaveChangesAsync();

        TempData["AttendanceSuccess"] =
            "Student marked as Absent.";

        return RedirectToManage(
            lesson
        );
    }


    // ==================================================
    // MANAGED LESSON RESOLUTION
    // ==================================================

    private async Task<ManagedLessonContext?>
        ResolveManagedLessonAsync(
            string teacherId,
            string timetableId,
            DateTime date,
            string? replacementId)
    {
        DateTime lessonDate =
            date.Date;

        if (
            !string.IsNullOrWhiteSpace(
                replacementId
            )
        )
        {
            ReplacementSession? replacement =
                await db.ReplacementSessions
                    .AsNoTracking()
                    .Include(r =>
                        r.SourceTimetable
                    )
                    .ThenInclude(t =>
                        t!.Term
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
                    .FirstOrDefaultAsync(r =>
                        r.ReplacementId ==
                            replacementId &&
                        r.TeacherId ==
                            teacherId &&
                        r.SourceTimetable !=
                            null &&
                        r.SourceTimetable.TimetableId ==
                            timetableId
                    );

            if (
                replacement ==
                    null ||
                replacement.SourceTimetable ==
                    null ||
                replacement.SourceTimetable.Term ==
                    null ||
                replacement.SourceTimetable.Term.Status !=
                    "Active" ||
                replacement.ReplacementDate.Date !=
                    lessonDate ||
                !string.Equals(
                    replacement.Status,
                    "Scheduled",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return null;
            }

            string replacementRoomName =
                "-";

            if (
                !string.IsNullOrWhiteSpace(
                    replacement.ClassroomId
                )
            )
            {
                replacementRoomName =
                    await db.Classrooms
                        .AsNoTracking()
                        .Where(c =>
                            c.ClassroomId ==
                                replacement.ClassroomId
                        )
                        .Select(c =>
                            c.RoomName
                        )
                        .FirstOrDefaultAsync()
                    ?? "-";
            }

            Timetable source =
                replacement.SourceTimetable;

            return new ManagedLessonContext
            {
                TimetableId =
                    source.TimetableId,

                ReplacementId =
                    replacement.ReplacementId,

                Date =
                    replacement.ReplacementDate.Date,

                ClassId =
                    source.ClassId,

                CourseName =
                    source.Course?.CourseName
                    ?? source.CourseId,

                ClassName =
                    source.ClassGroup?.ClassName
                    ?? source.ClassId,

                RoomName =
                    replacementRoomName,

                StartTime =
                    replacement.StartTime,

                EndTime =
                    replacement.EndTime
            };
        }

        Timetable? timetable =
            await db.Timetables
                .AsNoTracking()
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
                .FirstOrDefaultAsync(t =>
                    t.TimetableId ==
                        timetableId &&
                    t.TeacherId ==
                        teacherId &&
                    t.Term !=
                        null &&
                    t.Term.Status ==
                        "Active"
                );

        if (
            timetable ==
                null ||
            timetable.Term ==
                null ||
            lessonDate <
                timetable.Term.StartDate.Date ||
            lessonDate >
                timetable.Term.EndDate.Date ||
            lessonDate.DayOfWeek !=
                timetable.DayOfWeek
        )
        {
            return null;
        }

        TimetableException? exception =
            await db.TimetableExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.TimetableId ==
                        timetable.TimetableId &&
                    e.OccurrenceDate.Date ==
                        lessonDate &&
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
            return null;
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
            exception !=
                null &&
            !string.IsNullOrWhiteSpace(
                exception.OverrideClassroomId
            )
        )
        {
            roomName =
                await db.Classrooms
                    .AsNoTracking()
                    .Where(c =>
                        c.ClassroomId ==
                            exception.OverrideClassroomId
                    )
                    .Select(c =>
                        c.RoomName
                    )
                    .FirstOrDefaultAsync()
                ?? roomName;
        }

        return new ManagedLessonContext
        {
            TimetableId =
                timetable.TimetableId,

            ReplacementId =
                null,

            Date =
                lessonDate,

            ClassId =
                timetable.ClassId,

            CourseName =
                timetable.Course?.CourseName
                ?? timetable.CourseId,

            ClassName =
                timetable.ClassGroup?.ClassName
                ?? timetable.ClassId,

            RoomName =
                roomName,

            StartTime =
                startTime,

            EndTime =
                endTime
        };
    }


    private async Task<AttendanceSession>
        GetOrCreateManualSessionAsync(
            ManagedLessonContext lesson,
            DateTime now)
    {
        AttendanceSession? session =
            await db.AttendanceSessions
                .FirstOrDefaultAsync(s =>
                    s.TimetableId ==
                        lesson.TimetableId &&
                    s.SessionDate ==
                        lesson.Date
                );

        if (
            session !=
            null
        )
        {
            return session;
        }

        string systemCode =
            RandomNumberGenerator
                .GetInt32(
                    100000,
                    1000000
                )
                .ToString();

        session =
            new AttendanceSession
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
                    lesson.TimetableId,

                SessionDate =
                    lesson.Date,

                // This code is never shown to students because
                // the session is inactive. It only satisfies the
                // existing AttendanceSession database structure.
                Code =
                    systemCode,

                GeneratedAt =
                    now,

                ExpiresAt =
                    lesson.Date.Add(
                        lesson.EndTime
                    ),

                IsActive =
                    false
            };

        db.AttendanceSessions.Add(
            session
        );

        await db.SaveChangesAsync();

        return session;
    }


    private IActionResult RedirectToManage(
        ManagedLessonContext lesson)
    {
        return RedirectToAction(
            nameof(Manage),
            new
            {
                timetableId =
                    lesson.TimetableId,

                date =
                    lesson.Date
                        .ToString(
                            "yyyy-MM-dd"
                        ),

                replacementId =
                    lesson.ReplacementId
            }
        );
    }


    private class ManagedLessonContext
    {
        public string TimetableId { get; set; } =
            string.Empty;

        public string? ReplacementId { get; set; }

        public DateTime Date { get; set; }

        public string ClassId { get; set; } =
            string.Empty;

        public string CourseName { get; set; } =
            string.Empty;

        public string ClassName { get; set; } =
            string.Empty;

        public string RoomName { get; set; } =
            string.Empty;

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }
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