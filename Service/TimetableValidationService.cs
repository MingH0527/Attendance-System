using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Services;

public class TimetableValidationService :
    ITimetableValidationService
{
    private readonly DB _context;

    private static readonly TimeSpan EarliestStart =
        new(9, 0, 0);

    private static readonly TimeSpan LatestEnd =
        new(17, 0, 0);

    public TimetableValidationService(DB context)
    {
        _context = context;
    }

    public async Task<ValidationResult> ValidateAsync(
        TimetableRequest request)
    {
        if (!TimeSpan.TryParse(
                request.StartTime,
                out TimeSpan start) ||
            !TimeSpan.TryParse(
                request.EndTime,
                out TimeSpan end))
        {
            return ValidationResult.Fail(
                "Invalid start or end time format.");
        }

        if (start >= end)
        {
            return ValidationResult.Fail(
                "Start time must be before end time.");
        }

        if (start < EarliestStart || end > LatestEnd)
        {
            return ValidationResult.Fail(
                "Classes must be between 9:00 AM and 5:00 PM.");
        }

        bool isWeekday =
            request.DayOfWeek >= DayOfWeek.Monday &&
            request.DayOfWeek <= DayOfWeek.Friday;

        bool isWeekend =
            request.DayOfWeek == DayOfWeek.Saturday ||
            request.DayOfWeek == DayOfWeek.Sunday;

        if (isWeekday &&
            request.ClassType != ClassType.School)
        {
            return ValidationResult.Fail(
                "Monday to Friday can only be used for School classes.");
        }

        if (isWeekend &&
            request.ClassType != ClassType.Tuition)
        {
            return ValidationResult.Fail(
                "Saturday and Sunday can only be used for Tuition classes.");
        }

        bool teacherExists = await _context.Users.AnyAsync(
            u => u.UserId == request.TeacherId &&
                 u.Position == "Teacher");

        if (!teacherExists)
        {
            return ValidationResult.Fail(
                "The selected teacher does not exist.");
        }

        bool courseExists = await _context.Courses.AnyAsync(
            c => c.CourseId == request.CourseId);

        if (!courseExists)
        {
            return ValidationResult.Fail(
                "The selected course does not exist.");
        }

        bool classExists = await _context.ClassGroups.AnyAsync(
            c => c.ClassId == request.ClassId);

        if (!classExists)
        {
            return ValidationResult.Fail(
                "The selected class does not exist.");
        }

        if (request.ClassroomId.HasValue)
        {
            bool classroomExists =
                await _context.Classrooms.AnyAsync(
                    c => c.ClassroomId ==
                         request.ClassroomId.Value);

            if (!classroomExists)
            {
                return ValidationResult.Fail(
                    "The selected classroom does not exist.");
            }
        }

        var sameDayQuery = _context.Timetables
            .AsNoTracking()
            .Where(t =>
                t.DayOfWeek == request.DayOfWeek);

        if (request.TimetableId.HasValue)
        {
            sameDayQuery = sameDayQuery.Where(
                t => t.TimetableId !=
                     request.TimetableId.Value);
        }

        var sameDayEntries =
            await sameDayQuery.ToListAsync();

        static bool Overlaps(
            Timetable existing,
            TimeSpan newStart,
            TimeSpan newEnd)
        {
            return existing.StartTime < newEnd &&
                   newStart < existing.EndTime;
        }

        if (sameDayEntries.Any(t =>
                t.TeacherId == request.TeacherId &&
                Overlaps(t, start, end)))
        {
            return ValidationResult.Fail(
                "This teacher is already assigned during this time.");
        }

        if (sameDayEntries.Any(t =>
                t.ClassId == request.ClassId &&
                Overlaps(t, start, end)))
        {
            return ValidationResult.Fail(
                "This class already has another lesson during this time.");
        }

        if (request.ClassroomId.HasValue &&
            sameDayEntries.Any(t =>
                t.ClassroomId ==
                    request.ClassroomId.Value &&
                Overlaps(t, start, end)))
        {
            return ValidationResult.Fail(
                "This classroom is already occupied during this time.");
        }

        return ValidationResult.Success();
    }
}