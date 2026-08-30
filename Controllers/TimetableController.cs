using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security;
using System.Text;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Admin")]
public class TimetableController : Controller
{
    private readonly DB _db;
    private readonly ITimetableValidationService _validation;

    public TimetableController(DB db, ITimetableValidationService validation)
    {
        _db = db;
        _validation = validation;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;

        // TimetableV2 introduced TermId after older timetable rows already existed.
        // When there is exactly one live semester, safely attach legacy rows with no TermId
        // to that semester so the grid/export does not suddenly appear empty.
        var liveTerms = await _db.AcademicTerms
            .Where(t => t.Status != "Completed" && t.EndDate >= today)
            .OrderBy(t => t.StartDate)
            .ToListAsync();
        if (liveTerms.Count == 1 && await _db.Timetables.AnyAsync(t => t.TermId == null))
        {
            var legacyRows = await _db.Timetables.Where(t => t.TermId == null).ToListAsync();
            foreach (var row in legacyRows) row.TermId = liveTerms[0].TermId;
            await _db.SaveChangesAsync();
        }

        var expired = await _db.AcademicTerms.Where(t => t.Status != "Completed" && t.EndDate < today).ToListAsync();
        if (expired.Count > 0)
        {
            foreach (var t in expired) t.Status = "Completed";
            await _db.SaveChangesAsync();
        }

        var terms = await _db.AcademicTerms.AsNoTracking().OrderByDescending(t => t.StartDate).ToListAsync();
        var programs = await _db.AcademicPrograms.AsNoTracking().Include(p => p.Classes).OrderBy(p => p.ProgramCode).ToListAsync();

        TermDashboardDto Map(AcademicTerm t) => new()
        {
            TermId = t.TermId,
            Name = t.Name,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Status = t.Status,
            Programs = programs.Select(p => new ProgramDashboardDto
            {
                ProgramId = p.ProgramId,
                ProgramCode = p.ProgramCode,
                ProgramName = p.ProgramName,
                Classes = p.Classes.OrderBy(c => c.ClassName)
                    .Select(c => new ClassDashboardDto { ClassId = c.ClassId, ClassName = c.ClassName }).ToList()
            }).ToList()
        };

        return Json(new TimetableDashboardDto
        {
            Current = terms.Where(t => t.Status != "Completed").Select(Map).ToList(),
            Completed = terms.Where(t => t.Status == "Completed").Select(Map).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Programs() => Json(await _db.AcademicPrograms.AsNoTracking()
        .OrderBy(p => p.ProgramCode).Select(p => new { p.ProgramId, p.ProgramCode, p.ProgramName }).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Classes(string programId) => Json(await _db.ClassGroups.AsNoTracking()
        .Where(c => c.ProgramId == programId).OrderBy(c => c.ClassName)
        .Select(c => new SimpleLookupDto { Id = c.ClassId, Name = c.ClassName }).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Rooms() => Json(await _db.Classrooms.AsNoTracking()
        .OrderBy(r => r.RoomName).Select(r => new SimpleLookupDto { Id = r.ClassroomId, Name = r.RoomName }).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Assignments(string classId)
    {
        var cls = await _db.ClassGroups.AsNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId);
        if (cls is null) return NotFound();

        var mapped = await _db.ProgramCourses.AsNoTracking()
            .Where(pc => pc.ProgramId == cls.ProgramId).Select(pc => pc.CourseId).ToListAsync();

        var q = _db.TeacherCourses.AsNoTracking().Include(tc => tc.User).Include(tc => tc.Course)
            .Where(tc => tc.User != null && tc.User.Position == "Teacher");
        if (mapped.Count > 0) q = q.Where(tc => mapped.Contains(tc.CourseId));

        return Json(await q.OrderBy(tc => tc.Course!.CourseName).ThenBy(tc => tc.User!.Username)
            .Select(tc => new TeacherCourseLookupDto
            {
                TeacherId = tc.UserId,
                TeacherName = tc.User!.Username,
                CourseId = tc.CourseId,
                CourseName = tc.Course!.CourseName
            }).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Grid(string termId, string classId, string classType, DateTime? weekDate = null)
    {
        if (!Enum.TryParse<ClassType>(classType, true, out var type)) return BadRequest("Invalid schedule type.");
        var term = await _db.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.TermId == termId);
        if (term is null) return NotFound("Academic term not found.");

        var entries = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == termId && t.ClassId == classId && t.ClassType == type)
            .Include(t => t.Course).Include(t => t.ClassGroup).Include(t => t.Classroom).Include(t => t.Teacher)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime).ToListAsync();

        DateTime? weekStart = weekDate?.Date;
        var events = weekStart.HasValue
            ? await _db.AcademicCalendarEvents.AsNoTracking()
                .Where(e => e.TermId == termId && e.AffectsClasses && e.EventDate >= weekStart.Value && e.EventDate < weekStart.Value.AddDays(7)).ToListAsync()
            : new List<AcademicCalendarEvent>();

        var entryIds = entries.Select(x => x.TimetableId).ToList();
        var exceptions = weekStart.HasValue
            ? await _db.TimetableExceptions.AsNoTracking()
                .Where(e => entryIds.Contains(e.TimetableId) && e.OccurrenceDate >= weekStart.Value && e.OccurrenceDate < weekStart.Value.AddDays(7)).ToListAsync()
            : new List<TimetableException>();

        // Existing legacy conflicts are highlighted as Attention. New conflicts are blocked by validation.
        var allTermEntries = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == termId && t.ClassType == type)
            .ToListAsync();

        var result = entries.Select(t =>
        {
            string status = "Normal";
            string? reason = null;

            bool hasLegacyConflict = allTermEntries.Any(other =>
                other.TimetableId != t.TimetableId && other.DayOfWeek == t.DayOfWeek &&
                other.StartTime < t.EndTime && t.StartTime < other.EndTime &&
                (other.TeacherId == t.TeacherId || other.ClassId == t.ClassId ||
                 (!string.IsNullOrWhiteSpace(t.ClassroomId) && other.ClassroomId == t.ClassroomId)));

            if (hasLegacyConflict)
            {
                status = "Attention";
                reason = "Existing timetable conflict";
            }

            if (weekStart.HasValue)
            {
                var occurrence = OccurrenceDate(weekStart.Value, t.DayOfWeek);
                var ev = events.FirstOrDefault(e => e.EventDate.Date == occurrence.Date);
                var ex = exceptions.FirstOrDefault(e => e.TimetableId == t.TimetableId && e.OccurrenceDate.Date == occurrence.Date);

                if (ex?.Decision == "Cancel")
                {
                    status = "Cancelled";
                    reason = ex.Reason == "Holiday" ? "Holiday - Admin Cancelled" : (ex.Note ?? "Admin Cancelled");
                }
                else if (ev != null)
                {
                    status = "Attention";
                    reason = ex?.Decision == "Keep" ? $"{ev.Name} - Kept by Admin" : $"{ev.Name} - Pending Admin Decision";
                }
            }

            return new TimetableSlotDto
            {
                TermId = t.TermId,
                TimetableId = t.TimetableId,
                TeacherId = t.TeacherId,
                TeacherName = t.Teacher.Username,
                CourseId = t.CourseId,
                CourseName = t.Course?.CourseName ?? "",
                ClassId = t.ClassId,
                ClassName = t.ClassGroup?.ClassName ?? "",
                ClassroomId = t.ClassroomId,
                RoomName = t.Classroom?.RoomName,
                DayOfWeek = t.DayOfWeek,
                StartTime = t.StartTime.ToString(@"hh\:mm"),
                EndTime = t.EndTime.ToString(@"hh\:mm"),
                ClassType = t.ClassType,
                Status = status,
                StatusReason = reason
            };
        }).ToList();

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] TimetableRequest r)
    {
        var term = await _db.AcademicTerms.FirstOrDefaultAsync(t => t.TermId == r.TermId);
        if (term is null) return BadRequest("Please select an academic term.");
        if (term.Status == "Completed") return BadRequest("Completed semesters are read-only.");

        var v = await _validation.ValidateAsync(r);
        if (!v.IsValid) return BadRequest(v.ErrorMessage);

        Timetable? e = null;
        if (!string.IsNullOrWhiteSpace(r.TimetableId)) e = await _db.Timetables.FindAsync(r.TimetableId);
        if (e is null)
        {
            e = new Timetable { TimetableId = await FormattedIdGenerator.NextAsync(_db.Timetables, x => x.TimetableId, "TTB") };
            _db.Timetables.Add(e);
        }

        e.TermId = r.TermId;
        e.TeacherId = r.TeacherId;
        e.CourseId = r.CourseId;
        e.ClassId = r.ClassId;
        e.ClassroomId = r.ClassroomId;
        e.DayOfWeek = r.DayOfWeek;
        e.StartTime = TimeSpan.Parse(r.StartTime);
        e.EndTime = TimeSpan.Parse(r.EndTime);
        e.ClassType = r.ClassType;
        await _db.SaveChangesAsync();
        return Json(new { e.TimetableId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var e = await _db.Timetables.FindAsync(id);
        if (e is null) return NotFound();
        var term = await _db.AcademicTerms.FindAsync(e.TermId);
        if (term?.Status == "Completed") return BadRequest("Completed semesters are read-only.");
        _db.Timetables.Remove(e);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SetOccurrenceStatus([FromBody] OccurrenceStatusRequest r)
    {
        if (r.Status is not ("Normal" or "Cancelled")) return BadRequest("Status must be Normal or Cancelled.");
        var t = await _db.Timetables.FindAsync(r.TimetableId);
        if (t is null) return NotFound();
        var term = await _db.AcademicTerms.FindAsync(t.TermId);
        if (term is null) return BadRequest("Academic term not found.");
        if (term.Status == "Completed") return BadRequest("Completed semesters are read-only.");
        if (r.Date.Date < term.StartDate.Date || r.Date.Date > term.EndDate.Date) return BadRequest("The selected date is outside the semester.");
        if (r.Date.DayOfWeek != t.DayOfWeek) return BadRequest("The selected date does not match the timetable day.");

        var holiday = await _db.AcademicCalendarEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TermId == t.TermId && e.AffectsClasses && e.EventDate.Date == r.Date.Date);
        var ex = await _db.TimetableExceptions
            .FirstOrDefaultAsync(x => x.TimetableId == r.TimetableId && x.OccurrenceDate.Date == r.Date.Date);

        if (ex is null)
        {
            ex = new TimetableException
            {
                ExceptionId = await FormattedIdGenerator.NextAsync(_db.TimetableExceptions, x => x.ExceptionId, "TEX"),
                TimetableId = r.TimetableId,
                OccurrenceDate = r.Date.Date
            };
            _db.TimetableExceptions.Add(ex);
        }

        ex.Decision = r.Status == "Cancelled" ? "Cancel" : "Keep";
        ex.Reason = holiday is null ? "Manual" : "Holiday";
        ex.Note = r.Note;

        var sessions = await _db.ClassSessions
            .Where(x => x.TimetableId == r.TimetableId && x.SessionDate == r.Date.Date).ToListAsync();
        foreach (var session in sessions)
        {
            session.Status = r.Status == "Cancelled" ? "Cancelled" : "Scheduled";
            session.Reason = r.Status == "Cancelled" ? (holiday is null ? "Admin Cancelled" : "Holiday - Admin Cancelled") : null;
        }

        await _db.SaveChangesAsync();
        if (r.Status == "Cancelled")
            await AddNotification("ReplacementRequired", $"Cancelled lesson {r.TimetableId} on {r.Date:dd MMM yyyy} requires review for replacement.", t.TermId, r.TimetableId);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> HolidayDecision([FromBody] HolidayDecisionRequest r)
    {
        return await SetOccurrenceStatus(new OccurrenceStatusRequest
        {
            TimetableId = r.TimetableId,
            Date = r.Date,
            Status = r.Decision == "Cancel" ? "Cancelled" : "Normal",
            Note = r.Note
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateReplacement([FromBody] ReplacementRequest r)
    {
        var source = await _db.Timetables.AsNoTracking().FirstOrDefaultAsync(t => t.TimetableId == r.SourceTimetableId);
        if (source is null) return NotFound();
        if (!TimeSpan.TryParse(r.StartTime, out var s) || !TimeSpan.TryParse(r.EndTime, out var e) || s >= e)
            return BadRequest("Invalid replacement time.");

        var hours = (e - s).TotalHours;
        if (r.TargetClassType == ClassType.Tuition && (hours < 1 || hours > 4))
            return BadRequest("A Tuition replacement must be between 1 and 4 hours per day.");

        // Replacement uses separate rules and is intentionally excluded from normal School/Tuition hour quotas.
        var dow = r.Date.DayOfWeek;
        var conflicts = await _db.Timetables.AsNoTracking().Where(t =>
            t.TermId == source.TermId && t.DayOfWeek == dow && t.StartTime < e && s < t.EndTime &&
            (t.TeacherId == r.TeacherId || t.ClassId == source.ClassId ||
             (!string.IsNullOrEmpty(r.ClassroomId) && t.ClassroomId == r.ClassroomId))).AnyAsync();
        if (conflicts) return BadRequest("Replacement conflicts with an existing teacher, class, or classroom booking.");

        var rep = new ReplacementSession
        {
            ReplacementId = await FormattedIdGenerator.NextAsync(_db.ReplacementSessions, x => x.ReplacementId, "RPL"),
            SourceTimetableId = source.TimetableId,
            ReplacementDate = r.Date.Date,
            StartTime = s,
            EndTime = e,
            TargetClassType = r.TargetClassType,
            TeacherId = r.TeacherId,
            ClassroomId = r.ClassroomId,
            Status = "Scheduled",
            Reason = "Replacement"
        };
        _db.ReplacementSessions.Add(rep);

        var session = new ClassSession
        {
            SessionId = await FormattedIdGenerator.NextAsync(_db.ClassSessions, x => x.SessionId, "SES", 6),
            TermId = source.TermId ?? string.Empty,
            TimetableId = source.TimetableId,
            ReplacementId = rep.ReplacementId,
            SessionDate = r.Date.Date,
            StartTime = s,
            EndTime = e,
            Status = "Replacement",
            Reason = "Make-up session"
        };
        _db.ClassSessions.Add(session);
        await _db.SaveChangesAsync();
        return Json(new { rep.ReplacementId, session.SessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Notifications() => Json(await _db.TimetableNotifications.AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> CreateTerm([FromBody] AcademicTerm term)
    {
        var today = DateTime.Today;
        term.Name = (term.Name ?? string.Empty).Trim();
        term.StartDate = term.StartDate.Date;
        term.EndDate = term.EndDate.Date;

        if (term.Name.Length < 3 || term.Name.Length > 100)
            return BadRequest("Semester name must be between 3 and 100 characters.");
        if (term.StartDate < today)
            return BadRequest("Start date cannot be in the past.");
        if (term.EndDate < term.StartDate)
            return BadRequest("End date cannot be earlier than the start date.");
        if (term.EndDate > term.StartDate.AddYears(1))
            return BadRequest("Semester duration cannot exceed one year.");
        if (await _db.AcademicTerms.AnyAsync(t => t.Name.ToLower() == term.Name.ToLower()))
            return BadRequest("A semester with this name already exists.");
        if (await _db.AcademicTerms.AnyAsync(t => t.StartDate <= term.EndDate && term.StartDate <= t.EndDate))
            return BadRequest("Academic term dates overlap with an existing semester.");
        term.Status = term.EndDate < today ? "Completed" : term.StartDate > today ? "Upcoming" : "Active";
        term.TermId = await FormattedIdGenerator.NextAsync(_db.AcademicTerms, x => x.TermId, "TRM");
        _db.AcademicTerms.Add(term);

        int weekNo = 1;
        for (var start = term.StartDate; start <= term.EndDate; start = start.AddDays(7), weekNo++)
        {
            _db.AcademicWeeks.Add(new AcademicWeek
            {
                AcademicWeekId = await FormattedIdGenerator.NextAsync(_db.AcademicWeeks, x => x.AcademicWeekId, "AWK"),
                TermId = term.TermId,
                WeekNumber = weekNo,
                StartDate = start,
                EndDate = start.AddDays(6) > term.EndDate ? term.EndDate : start.AddDays(6),
                WeekType = "Teaching"
            });
        }

        await _db.SaveChangesAsync();
        return Json(term);
    }

    [HttpPost]
    public async Task<IActionResult> CompleteTerm(string termId)
    {
        var t = await _db.AcademicTerms.FindAsync(termId);
        if (t is null) return NotFound();
        t.Status = "Completed";
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> GenerateWeeks(string termId, int teachingWeeks = 14)
    {
        var t = await _db.AcademicTerms.FindAsync(termId);
        if (t is null) return NotFound();
        if (teachingWeeks < 1 || teachingWeeks > 30) return BadRequest("Teaching weeks must be between 1 and 30.");
        var old = await _db.AcademicWeeks.Where(w => w.TermId == termId).ToListAsync();
        _db.AcademicWeeks.RemoveRange(old);
        for (int i = 1; i <= teachingWeeks; i++)
        {
            var start = t.StartDate.Date.AddDays((i - 1) * 7);
            var end = start.AddDays(6) > t.EndDate.Date ? t.EndDate.Date : start.AddDays(6);
            if (start > t.EndDate.Date) break;
            _db.AcademicWeeks.Add(new AcademicWeek
            {
                AcademicWeekId = await FormattedIdGenerator.NextAsync(_db.AcademicWeeks, x => x.AcademicWeekId, "AWK"),
                TermId = termId,
                WeekNumber = i,
                StartDate = start,
                EndDate = end,
                WeekType = "Teaching"
            });
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Weeks(string termId) => Json(await _db.AcademicWeeks.AsNoTracking()
        .Where(w => w.TermId == termId).OrderBy(w => w.StartDate).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> AddCalendarEvent([FromBody] AcademicCalendarEvent e)
    {
        var term = await _db.AcademicTerms.FirstOrDefaultAsync(t => t.TermId == e.TermId);
        if (term is null) return BadRequest("Academic term not found.");
        if (e.EventDate.Date < DateTime.Today) return BadRequest("Past dates cannot be added as new holidays.");
        if (e.EventDate.Date < term.StartDate.Date || e.EventDate.Date > term.EndDate.Date)
            return BadRequest("Holiday date must be inside the selected semester.");
        if (string.IsNullOrWhiteSpace(e.Name)) return BadRequest("Holiday name is required.");
        if (await _db.AcademicCalendarEvents.AnyAsync(x => x.TermId == e.TermId && x.EventDate.Date == e.EventDate.Date && x.Name == e.Name))
            return BadRequest("This holiday already exists.");

        e.CalendarEventId = await FormattedIdGenerator.NextAsync(_db.AcademicCalendarEvents, x => x.CalendarEventId, "CAL");
        e.EventDate = e.EventDate.Date;
        e.AffectsClasses = true;
        _db.AcademicCalendarEvents.Add(e);
        await _db.SaveChangesAsync();
        await AddNotification("HolidayAffected", $"{e.Name} on {e.EventDate:dd MMM yyyy} may affect scheduled classes.", e.TermId, null);
        return Json(e);
    }

    [HttpGet]
    public async Task<IActionResult> Calendar(string termId) => Json(await _db.AcademicCalendarEvents.AsNoTracking()
        .Where(e => e.TermId == termId).OrderBy(e => e.EventDate).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> LoadMalaysia2026Holidays(string termId)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return BadRequest("Academic term not found.");
        if (term.Status == "Completed") return BadRequest("Completed semesters are read-only.");

        var holidays = new (DateTime Date, string Name)[]
        {
            (new DateTime(2026, 2, 17), "Chinese New Year"),
            (new DateTime(2026, 2, 18), "Chinese New Year (Day 2)"),
            (new DateTime(2026, 3, 21), "Hari Raya Aidilfitri"),
            (new DateTime(2026, 3, 22), "Hari Raya Aidilfitri (Day 2)"),
            (new DateTime(2026, 5, 1), "Labour Day"),
            (new DateTime(2026, 5, 27), "Hari Raya Haji"),
            (new DateTime(2026, 5, 31), "Wesak Day"),
            (new DateTime(2026, 6, 1), "Yang di-Pertuan Agong's Birthday"),
            (new DateTime(2026, 6, 17), "Awal Muharram"),
            (new DateTime(2026, 8, 25), "Maulidur Rasul"),
            (new DateTime(2026, 8, 31), "National Day"),
            (new DateTime(2026, 9, 16), "Malaysia Day"),
            (new DateTime(2026, 11, 8), "Deepavali"),
            (new DateTime(2026, 12, 25), "Christmas Day")
        };

        int added = 0;
        foreach (var h in holidays.Where(h => h.Date >= term.StartDate.Date && h.Date <= term.EndDate.Date))
        {
            if (await _db.AcademicCalendarEvents.AnyAsync(x => x.TermId == termId && x.EventDate.Date == h.Date.Date && x.Name == h.Name)) continue;
            _db.AcademicCalendarEvents.Add(new AcademicCalendarEvent
            {
                CalendarEventId = await FormattedIdGenerator.NextAsync(_db.AcademicCalendarEvents, x => x.CalendarEventId, "CAL"),
                TermId = termId,
                EventDate = h.Date,
                Name = h.Name,
                EventType = "PublicHoliday",
                AffectsClasses = true
            });
            added++;
        }
        await _db.SaveChangesAsync();
        return Json(new { added });
    }

    [HttpPost]
    public async Task<IActionResult> ImportCalendarEvents(string termId, IFormFile file)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return BadRequest("Academic term not found.");
        if (term.Status == "Completed") return BadRequest("Completed semesters are read-only.");
        if (file is null || file.Length == 0) return BadRequest("Please drop a non-empty .txt or .csv file.");
        if (file.Length > 1024 * 1024) return BadRequest("Holiday import file must be smaller than 1 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".txt" or ".csv")) return BadRequest("Only .txt and .csv files are accepted.");

        using var reader = new StreamReader(file.OpenReadStream());
        string? line;
        int lineNo = 0, added = 0, skipped = 0;
        var errors = new List<string>();
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNo++;
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            if (lineNo == 1 && line.Contains("date", StringComparison.OrdinalIgnoreCase) && line.Contains("name", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Contains('|') ? line.Split('|', 3) : line.Contains('\t') ? line.Split('\t', 3) : line.Split(',', 3);
            if (parts.Length < 2) { errors.Add($"Line {lineNo}: expected Date | Name | Type."); continue; }
            if (!DateTime.TryParseExact(parts[0].Trim(), new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            { errors.Add($"Line {lineNo}: invalid date '{parts[0].Trim()}'."); continue; }

            var name = parts[1].Trim();
            var type = parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : "PublicHoliday";
            if (name.Length < 2 || name.Length > 150) { errors.Add($"Line {lineNo}: holiday name must be 2-150 characters."); continue; }
            if (date.Date < term.StartDate.Date || date.Date > term.EndDate.Date) { skipped++; continue; }
            if (await _db.AcademicCalendarEvents.AnyAsync(x => x.TermId == termId && x.EventDate.Date == date.Date && x.Name == name)) { skipped++; continue; }

            _db.AcademicCalendarEvents.Add(new AcademicCalendarEvent
            {
                CalendarEventId = await FormattedIdGenerator.NextAsync(_db.AcademicCalendarEvents, x => x.CalendarEventId, "CAL"),
                TermId = termId,
                EventDate = date.Date,
                Name = name,
                EventType = type.Length > 30 ? type[..30] : type,
                AffectsClasses = true
            });
            await _db.SaveChangesAsync();
            added++;
        }

        return Json(new { added, skipped, errors = errors.Take(8).ToList() });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCalendarEvent(string id)
    {
        var e = await _db.AcademicCalendarEvents.FindAsync(id);
        if (e is null) return NotFound();
        var term = await _db.AcademicTerms.FindAsync(e.TermId);
        if (term?.Status == "Completed") return BadRequest("Completed semesters are read-only.");
        _db.AcademicCalendarEvents.Remove(e);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> MapProgramCourse(string programId, string courseId)
    {
        if (await _db.ProgramCourses.AnyAsync(x => x.ProgramId == programId && x.CourseId == courseId))
            return BadRequest("Subject is already mapped to this program.");
        var pc = new ProgramCourse
        {
            ProgramCourseId = await FormattedIdGenerator.NextAsync(_db.ProgramCourses, x => x.ProgramCourseId, "PCR"),
            ProgramId = programId,
            CourseId = courseId
        };
        _db.ProgramCourses.Add(pc);
        await _db.SaveChangesAsync();
        return Json(pc);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateSessions(string termId, DateTime from, DateTime to)
    {
        if (to < from) return BadRequest("Invalid date range.");
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return NotFound();
        from = from.Date < term.StartDate.Date ? term.StartDate.Date : from.Date;
        to = to.Date > term.EndDate.Date ? term.EndDate.Date : to.Date;
        var templates = await _db.Timetables.AsNoTracking().Where(t => t.TermId == termId).ToListAsync();
        var holidays = await _db.AcademicCalendarEvents.AsNoTracking()
            .Where(e => e.TermId == termId && e.AffectsClasses && e.EventDate >= from && e.EventDate <= to).ToListAsync();
        int made = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            foreach (var t in templates.Where(x => x.DayOfWeek == d.DayOfWeek))
            {
                if (await _db.ClassSessions.AnyAsync(s => s.TimetableId == t.TimetableId && s.SessionDate == d)) continue;
                var h = holidays.FirstOrDefault(x => x.EventDate.Date == d);
                _db.ClassSessions.Add(new ClassSession
                {
                    SessionId = await FormattedIdGenerator.NextAsync(_db.ClassSessions, x => x.SessionId, "SES", 6),
                    TermId = termId,
                    TimetableId = t.TimetableId,
                    SessionDate = d,
                    StartTime = t.StartTime,
                    EndTime = t.EndTime,
                    Status = h is null ? "Scheduled" : "HolidayAffected",
                    Reason = h?.Name
                });
                made++;
            }
        }
        await _db.SaveChangesAsync();
        return Json(new { created = made });
    }

    [HttpPost]
    public async Task<IActionResult> RefreshTimetableNotifications(string termId)
    {
        var today = DateTime.Today;
        var upcoming = await _db.ReplacementSessions.AsNoTracking()
            .Where(r => r.ReplacementDate >= today && r.ReplacementDate <= today.AddDays(1)).ToListAsync();
        foreach (var r in upcoming)
            await AddNotification("UpcomingReplacement", $"Replacement {r.ReplacementId} is scheduled for {r.ReplacementDate:dd MMM} {r.StartTime:hh\\:mm}-{r.EndTime:hh\\:mm}.", termId, r.SourceTimetableId);
        return Ok();
    }

    private async Task AddNotification(string type, string message, string? termId, string? timetableId)
    {
        if (await _db.TimetableNotifications.AnyAsync(n => n.Type == type && n.Message == message && !n.IsRead)) return;
        _db.TimetableNotifications.Add(new TimetableNotification
        {
            NotificationId = await FormattedIdGenerator.NextAsync(_db.TimetableNotifications, x => x.NotificationId, "NTF"),
            Type = type,
            Message = message,
            TermId = termId,
            TimetableId = timetableId
        });
        await _db.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Export(string termId, string classId, string classType, string format = "csv", DateTime? weekDate = null)
    {
        var data = (JsonResult)await Grid(termId, classId, classType, weekDate);
        var slots = (data.Value as IEnumerable<TimetableSlotDto>)?.ToList() ?? new();
        var cls = await _db.ClassGroups.AsNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId);
        var term = await _db.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.TermId == termId);
        string className = cls?.ClassName ?? classId;

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder("Day,Start,End,Subject,Teacher,Room,Status\r\n");
            foreach (var x in slots)
                sb.AppendLine($"{x.DayOfWeek},{x.StartTime},{x.EndTime},\"{Csv(x.CourseName)}\",\"{Csv(x.TeacherName)}\",\"{Csv(x.RoomName ?? "")}\",{x.Status}");
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", $"timetable-{className}.csv");
        }

        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
        {
            string xml = "<?xml version=\"1.0\"?><Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"><Worksheet ss:Name=\"Timetable\"><Table>" +
                         Row("Day", "Start", "End", "Subject", "Teacher", "Room", "Status") +
                         string.Concat(slots.Select(x => Row(x.DayOfWeek.ToString(), x.StartTime, x.EndTime, x.CourseName, x.TeacherName, x.RoomName ?? "", x.Status))) +
                         "</Table></Worksheet></Workbook>";
            return File(Encoding.UTF8.GetBytes(xml), "application/vnd.ms-excel", $"timetable-{className}.xls");
        }

        var pdf = TimetablePdf(term?.Name ?? termId, className, classType, weekDate, slots);
        return File(pdf, "application/pdf", $"timetable-{className}.pdf");
    }

    private static DateTime OccurrenceDate(DateTime weekStart, DayOfWeek day)
    {
        int delta = ((int)day - (int)weekStart.DayOfWeek + 7) % 7;
        return weekStart.AddDays(delta).Date;
    }

    private static string Csv(string text) => text.Replace("\"", "\"\"");
    private static string Row(params string[] cells) => "<Row>" + string.Concat(cells.Select(c => $"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(c)}</Data></Cell>")) + "</Row>";

    private static byte[] TimetablePdf(string termName, string className, string classType, DateTime? weekDate, List<TimetableSlotDto> slots)
    {
        // A4 landscape. The PDF is drawn directly so the export does not depend on browser print/CSS.
        const double pageW = 842, pageH = 595;
        const double margin = 26, dayW = 76, headerH = 42;
        double tableW = pageW - margin * 2;
        double hourW = (tableW - dayW) / 8.0;

        var days = classType.Equals("Tuition", StringComparison.OrdinalIgnoreCase)
            ? new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }
            : new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        double rowH = classType.Equals("Tuition", StringComparison.OrdinalIgnoreCase) ? 156 : 78;
        double tableTop = pageH - 116;
        var c = new StringBuilder();

        void SetFill(double r, double g, double b) => c.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} rg\n", r, g, b);
        void SetStroke(double r, double g, double b) => c.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} RG\n", r, g, b);
        void FillRect(double x, double y, double w, double h, double r, double g, double b)
        {
            SetFill(r, g, b);
            c.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##} {1:0.##} {2:0.##} {3:0.##} re f\n", x, y, w, h);
        }
        void StrokeRect(double x, double y, double w, double h, double r = 0.82, double g = 0.87, double b = 0.92, double width = 0.7)
        {
            SetStroke(r, g, b);
            c.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##} w {1:0.##} {2:0.##} {3:0.##} {4:0.##} re S\n", width, x, y, w, h);
        }
        void Text(double x, double y, double size, string value, bool bold = false, double r = 0.08, double g = 0.13, double b = 0.20)
        {
            SetFill(r, g, b);
            c.Append("BT ").Append(bold ? "/F2 " : "/F1 ")
                .Append(size.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Tf ")
                .Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                .Append(y.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Td (")
                .Append(PdfEscape(value)).Append(") Tj ET\n");
        }
        void Dot(double x, double y, double r, double g, double b)
        {
            FillRect(x, y, 5, 5, r, g, b);
        }

        // Page background and brand header.
        FillRect(0, 0, pageW, pageH, 0.972, 0.981, 0.992);
        FillRect(0, pageH - 8, pageW, 8, 0.02, 0.55, 0.70);

        Text(margin, pageH - 43, 22, "TIMETABLE", true, 0.03, 0.12, 0.22);
        Text(margin, pageH - 64, 10, $"{termName}  |  {className}  |  {classType}", false, 0.31, 0.39, 0.50);
        Text(margin, pageH - 80, 8.5,
            weekDate.HasValue ? $"Week of {weekDate.Value:dd MMM yyyy}" : "Weekly Template",
            false, 0.42, 0.49, 0.58);

        // Small legend on the right.
        double legendY = pageH - 68;
        double legendX = pageW - margin - 250;
        Dot(legendX, legendY, 0.10, 0.63, 0.72); Text(legendX + 10, legendY - 1, 7.5, "Normal", false, 0.31, 0.39, 0.50);
        Dot(legendX + 74, legendY, 0.96, 0.66, 0.12); Text(legendX + 84, legendY - 1, 7.5, "Attention", false, 0.31, 0.39, 0.50);
        Dot(legendX + 164, legendY, 0.90, 0.20, 0.24); Text(legendX + 174, legendY - 1, 7.5, "Cancelled", false, 0.31, 0.39, 0.50);

        double yHeader = tableTop - headerH;

        // Header row.
        FillRect(margin, yHeader, dayW, headerH, 0.055, 0.31, 0.50);
        StrokeRect(margin, yHeader, dayW, headerH, 0.055, 0.31, 0.50, 0.8);
        Text(margin + 11, yHeader + 17, 8.5, "Day / Time", true, 1, 1, 1);

        for (int i = 0; i < 8; i++)
        {
            double x = margin + dayW + i * hourW;
            FillRect(x, yHeader, hourW, headerH, 0.93, 0.96, 0.985);
            StrokeRect(x, yHeader, hourW, headerH, 0.78, 0.84, 0.90, 0.7);
            Text(x + 6, yHeader + 23, 7.6, HourLabel(9 + i), true, 0.08, 0.18, 0.30);
            Text(x + 6, yHeader + 10, 6.4, "to " + HourLabel(10 + i), false, 0.40, 0.47, 0.56);
        }

        for (int di = 0; di < days.Length; di++)
        {
            var day = days[di];
            double y = yHeader - (di + 1) * rowH;
            bool alternate = di % 2 == 1;

            FillRect(margin, y, dayW, rowH, alternate ? 0.945 : 0.965, alternate ? 0.966 : 0.979, alternate ? 0.985 : 0.992);
            StrokeRect(margin, y, dayW, rowH, 0.78, 0.84, 0.90, 0.7);
            Text(margin + 11, y + rowH / 2 - 3, 8.4, day.ToString(), true, 0.08, 0.18, 0.30);

            for (int i = 0; i < 8; i++)
            {
                double x = margin + dayW + i * hourW;
                FillRect(x, y, hourW, rowH,
                    alternate ? 0.982 : 1.0,
                    alternate ? 0.988 : 1.0,
                    alternate ? 0.995 : 1.0);
                StrokeRect(x, y, hourW, rowH, 0.84, 0.88, 0.92, 0.55);
            }

            var daySlots = MergeForPdf(slots.Where(s => s.DayOfWeek == day).ToList());
            foreach (var s in daySlots)
            {
                if (!TimeSpan.TryParse(s.StartTime, out var start) || !TimeSpan.TryParse(s.EndTime, out var end)) continue;
                int startIndex = start.Hours - 9;
                int span = Math.Max(1, (int)Math.Ceiling((end - start).TotalHours));
                if (startIndex < 0 || startIndex > 7) continue;
                span = Math.Min(span, 8 - startIndex);

                double x = margin + dayW + startIndex * hourW;
                double w = span * hourW;
                double bx = x + 3, by = y + 5, bw = w - 6, bh = rowH - 10;

                bool cancelled = s.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
                bool attention = s.Status.Equals("Attention", StringComparison.OrdinalIgnoreCase);
                if (cancelled)
                {
                    FillRect(bx, by, bw, bh, 1.00, 0.90, 0.91);
                    FillRect(bx, by, 4, bh, 0.90, 0.20, 0.24);
                    StrokeRect(bx, by, bw, bh, 0.96, 0.70, 0.72, 0.75);
                }
                else if (attention)
                {
                    FillRect(bx, by, bw, bh, 1.00, 0.96, 0.82);
                    FillRect(bx, by, 4, bh, 0.96, 0.66, 0.12);
                    StrokeRect(bx, by, bw, bh, 0.94, 0.80, 0.45, 0.75);
                }
                else
                {
                    FillRect(bx, by, bw, bh, 0.82, 0.96, 0.97);
                    FillRect(bx, by, 4, bh, 0.03, 0.58, 0.70);
                    StrokeRect(bx, by, bw, bh, 0.55, 0.84, 0.87, 0.75);
                }

                int maxChars = Math.Max(11, (int)(bw / 5.1));
                var courseLines = WrapPdf(s.CourseName, maxChars).ToList();
                var detailLines = new List<string>();
                detailLines.AddRange(WrapPdf(s.TeacherName, maxChars));
                if (!string.IsNullOrWhiteSpace(s.RoomName)) detailLines.AddRange(WrapPdf(s.RoomName!, maxChars));
                if (s.Status != "Normal") detailLines.AddRange(WrapPdf(s.Status, maxChars));

                double ty = by + bh - 15;
                foreach (var line in courseLines)
                {
                    if (ty < by + 9) break;
                    Text(bx + 10, ty, 7.7, line, true, 0.04, 0.22, 0.30);
                    ty -= 10;
                }
                foreach (var line in detailLines)
                {
                    if (ty < by + 9) break;
                    Text(bx + 10, ty, 6.8, line, false, 0.17, 0.29, 0.36);
                    ty -= 9;
                }
            }
        }

        Text(margin, 14, 6.8, $"Generated timetable · {DateTime.Now:dd MMM yyyy HH:mm}", false, 0.48, 0.54, 0.62);

        string stream = c.ToString();
        var objects = new List<string>
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R>>endobj",
            "2 0 obj<< /Type /Pages /Kids[3 0 R] /Count 1>>endobj",
            $"3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox[0 0 {pageW} {pageH}] /Resources<< /Font<< /F1 4 0 R /F2 5 0 R>>>> /Contents 6 0 R>>endobj",
            "4 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica>>endobj",
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold>>endobj",
            $"6 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)}>>stream\n{stream}\nendstream endobj"
        };

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var o in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append(o).Append('\n');
        }
        int xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets.Skip(1)) sb.Append(o.ToString("D10")).Append(" 00000 n \n");
        sb.Append($"trailer<< /Size {objects.Count + 1} /Root 1 0 R>>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static List<TimetableSlotDto> MergeForPdf(List<TimetableSlotDto> entries)
    {
        var result = new List<TimetableSlotDto>();
        foreach (var e in entries.OrderBy(x => x.StartTime))
        {
            var p = result.LastOrDefault();
            if (p != null && p.EndTime == e.StartTime && p.CourseId == e.CourseId && p.TeacherId == e.TeacherId &&
                (p.ClassroomId ?? "") == (e.ClassroomId ?? "") && p.Status == e.Status)
            {
                p.EndTime = e.EndTime;
            }
            else
            {
                result.Add(new TimetableSlotDto
                {
                    TermId = e.TermId,
                    TimetableId = e.TimetableId,
                    TeacherId = e.TeacherId,
                    TeacherName = e.TeacherName,
                    CourseId = e.CourseId,
                    CourseName = e.CourseName,
                    ClassId = e.ClassId,
                    ClassName = e.ClassName,
                    ClassroomId = e.ClassroomId,
                    RoomName = e.RoomName,
                    DayOfWeek = e.DayOfWeek,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    ClassType = e.ClassType,
                    Status = e.Status,
                    StatusReason = e.StatusReason
                });
            }
        }
        return result;
    }

    private static IEnumerable<string> WrapPdf(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > maxChars)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private static string HourLabel(int hour)
    {
        string suffix = hour >= 12 ? "PM" : "AM";
        int display = hour > 12 ? hour - 12 : hour;
        return $"{display}:00 {suffix}";
    }

    private static string PdfEscape(string text) => text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
