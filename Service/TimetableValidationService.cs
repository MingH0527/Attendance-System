using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Services;

public class TimetableValidationService : ITimetableValidationService
{
    private readonly DB _context;
    private static readonly TimeSpan EarliestStart = new(9, 0, 0);
    private static readonly TimeSpan LatestEnd = new(17, 0, 0);

    public TimetableValidationService(DB context)
    {
        _context = context;
    }

    public async Task<ValidationResult> ValidateAsync(TimetableRequest request)
    {
        if (!TimeSpan.TryParse(request.StartTime, out var start) ||
            !TimeSpan.TryParse(request.EndTime, out var end))
            return ValidationResult.Fail("Invalid start or end time format.");

        if (start >= end)
            return ValidationResult.Fail("Start time must be before end time.");

        if (start < EarliestStart || end > LatestEnd)
            return ValidationResult.Fail("Classes must be between 9:00 AM and 5:00 PM.");

        if ((end - start).TotalMinutes != 60)
            return ValidationResult.Fail("Timetable editing uses one-hour slots only.");

        bool weekday = request.DayOfWeek >= DayOfWeek.Monday && request.DayOfWeek <= DayOfWeek.Friday;
        bool weekend = request.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (!weekday && !weekend)
            return ValidationResult.Fail("Invalid timetable day.");

        if (weekday && request.ClassType != ClassType.School)
            return ValidationResult.Fail("Monday to Friday can only be used for School classes.");

        if (weekend && request.ClassType != ClassType.Tuition)
            return ValidationResult.Fail("Saturday and Sunday can only be used for Tuition classes.");

        var selectedClass = await _context.ClassGroups.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClassId == request.ClassId);
        if (selectedClass == null)
            return ValidationResult.Fail("The selected class does not exist.");

        bool teacherExists = await _context.Users.AsNoTracking()
            .AnyAsync(u => u.UserId == request.TeacherId && u.Position == "Teacher");
        if (!teacherExists)
            return ValidationResult.Fail("The selected teacher does not exist.");

        bool courseExists = await _context.Courses.AsNoTracking()
            .AnyAsync(c => c.CourseId == request.CourseId);
        if (!courseExists)
            return ValidationResult.Fail("The selected subject does not exist.");

        if (!string.IsNullOrWhiteSpace(request.ClassroomId))
        {
            bool classroomExists = await _context.Classrooms.AsNoTracking()
                .AnyAsync(c => c.ClassroomId == request.ClassroomId);
            if (!classroomExists)
                return ValidationResult.Fail("The selected classroom does not exist.");
        }

        bool teacherCanTeachCourse = await _context.TeacherCourses.AsNoTracking()
            .AnyAsync(tc => tc.UserId == request.TeacherId && tc.CourseId == request.CourseId);
        if (!teacherCanTeachCourse)
            return ValidationResult.Fail("This teacher is not registered for the selected subject.");

        bool programHasMappings = await _context.ProgramCourses.AsNoTracking()
            .AnyAsync(pc => pc.ProgramId == selectedClass.ProgramId);
        if (programHasMappings)
        {
            bool belongs = await _context.ProgramCourses.AsNoTracking()
                .AnyAsync(pc => pc.ProgramId == selectedClass.ProgramId && pc.CourseId == request.CourseId);
            if (!belongs)
                return ValidationResult.Fail("This subject is not assigned to the class's academic program.");
        }

        var termId = request.TermId;
        var query = _context.Timetables.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(termId))
            query = query.Where(t => t.TermId == termId);
        if (!string.IsNullOrWhiteSpace(request.TimetableId))
            query = query.Where(t => t.TimetableId != request.TimetableId);

        var allTermSlots = await query.ToListAsync();
        var sameDay = allTermSlots.Where(t => t.DayOfWeek == request.DayOfWeek).ToList();

        static bool Overlap(Timetable t, TimeSpan s, TimeSpan e) => t.StartTime < e && s < t.EndTime;

        if (sameDay.Any(t => t.TeacherId == request.TeacherId && Overlap(t, start, end)))
            return ValidationResult.Fail("Teacher conflict: this teacher is already assigned during this time.");

        if (sameDay.Any(t => t.ClassId == request.ClassId && Overlap(t, start, end)))
            return ValidationResult.Fail("Class conflict: this class already has another lesson during this time.");

        if (!string.IsNullOrWhiteSpace(request.ClassroomId) &&
            sameDay.Any(t => t.ClassroomId == request.ClassroomId && Overlap(t, start, end)))
            return ValidationResult.Fail("Classroom conflict: this classroom is already occupied during this time.");

        double newHours = (end - start).TotalHours;

        double teacherDailyHours = sameDay
            .Where(t => t.TeacherId == request.TeacherId && t.ClassType == request.ClassType)
            .Sum(t => (t.EndTime - t.StartTime).TotalHours) + newHours;

        double teacherDailyLimit = request.ClassType == ClassType.School ? 6 : 4;
        if (teacherDailyHours > teacherDailyLimit)
            return ValidationResult.Fail(
                $"Teacher daily-hour limit: a teacher cannot teach more than {teacherDailyLimit:0} hours per day for {request.ClassType}.");

        var classDaySlots = sameDay
            .Where(t => t.ClassId == request.ClassId && t.ClassType == request.ClassType)
            .Select(t => (StartTime: t.StartTime, EndTime: t.EndTime, CourseId: t.CourseId))
            .Append((StartTime: start, EndTime: end, CourseId: request.CourseId))
            .OrderBy(x => x.StartTime)
            .ToList();

        double classDailyHours = classDaySlots.Sum(x => (x.EndTime - x.StartTime).TotalHours);
        double classDailyLimit = request.ClassType == ClassType.School ? 7 : 3;
        if (classDailyHours > classDailyLimit)
            return ValidationResult.Fail(
                $"Student daily-hour limit: a class cannot have more than {classDailyLimit:0} hours per day for {request.ClassType}.");

        var classPeriodSlots = allTermSlots
            .Where(t => t.ClassId == request.ClassId && t.ClassType == request.ClassType)
            .ToList();
        double classPeriodHours = classPeriodSlots.Sum(t => (t.EndTime - t.StartTime).TotalHours) + newHours;
        double classPeriodLimit = request.ClassType == ClassType.School ? 32 : 5;
        string periodName = request.ClassType == ClassType.School ? "Monday-to-Friday" : "Saturday-and-Sunday";
        if (classPeriodHours > classPeriodLimit)
            return ValidationResult.Fail(
                $"Student total-hour limit: a class cannot exceed {classPeriodLimit:0} {request.ClassType} hours across {periodName}.");

        double currentRun = 0, maxRun = 0;
        TimeSpan? previousEnd = null;
        foreach (var slot in classDaySlots)
        {
            double hours = (slot.EndTime - slot.StartTime).TotalHours;
            currentRun = previousEnd.HasValue && previousEnd.Value == slot.StartTime ? currentRun + hours : hours;
            maxRun = Math.Max(maxRun, currentRun);
            previousEnd = slot.EndTime;
        }
        if (maxRun > 4)
            return ValidationResult.Fail("Consecutive-hour limit: a class cannot have more than 4 continuous teaching hours.");

        var sameSubjectSlots = classDaySlots.Where(x => x.CourseId == request.CourseId).OrderBy(x => x.StartTime).ToList();
        currentRun = 0; maxRun = 0; previousEnd = null;
        foreach (var slot in sameSubjectSlots)
        {
            double hours = (slot.EndTime - slot.StartTime).TotalHours;
            currentRun = previousEnd.HasValue && previousEnd.Value == slot.StartTime ? currentRun + hours : hours;
            maxRun = Math.Max(maxRun, currentRun);
            previousEnd = slot.EndTime;
        }
        if (maxRun > 3)
            return ValidationResult.Fail("Same-subject limit: the same subject cannot run for more than 3 consecutive hours.");

        // Replacement sessions are intentionally not included in the normal School/Tuition quotas above.
        // Replacement validation is handled separately by TimetableController.CreateReplacement.
        return ValidationResult.Success();
    }
}
