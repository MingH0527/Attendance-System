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
    private readonly TimetablePdfService _timetablePdfService;

    public TimetableController(
        DB db,
        ITimetableValidationService validation,
        TimetablePdfService timetablePdfService)
    {
        _db = db;
        _validation = validation;
        _timetablePdfService = timetablePdfService;
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

        byte[] pdf = _timetablePdfService.Generate(
            term?.Name ?? termId,
            className,
            classType,
            weekDate,
            slots);

        return File(pdf, "application/pdf", $"timetable-{className}.pdf");
    }

    private static DateTime OccurrenceDate(DateTime weekStart, DayOfWeek day)
    {
        int delta = ((int)day - (int)weekStart.DayOfWeek + 7) % 7;
        return weekStart.AddDays(delta).Date;
    }

    private static string Csv(string text) => text.Replace("\"", "\"\"");
    private static string Row(params string[] cells) => "<Row>" + string.Concat(cells.Select(c => $"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(c)}</Data></Cell>")) + "</Row>";

}
