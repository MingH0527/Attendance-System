using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security;
using System.Security.Claims;
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
    private static readonly string[] FixedRoomNames = { "LR-101", "LR-102", "LR-103", "LR-104", "LR-105", "LR-106" };

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
        await SyncSemesterStatusesAsync(today);

        // TimetableV2 introduced TermId after older timetable rows already existed.
        // Only attach legacy rows when there is exactly one usable semester.
        var liveTerms = await _db.AcademicTerms
            .Where(t => t.Status != "Warning" && t.Status != "Abandoned")
            .OrderBy(t => t.StartDate)
            .ToListAsync();
        if (liveTerms.Count == 1 && await _db.Timetables.AnyAsync(t => t.TermId == null))
        {
            var legacyRows = await _db.Timetables.Where(t => t.TermId == null).ToListAsync();
            foreach (var row in legacyRows) row.TermId = liveTerms[0].TermId;
            await _db.SaveChangesAsync();
        }

        var terms = await _db.AcademicTerms.AsNoTracking().OrderBy(t => t.StartDate).ToListAsync();
        var programs = await _db.AcademicPrograms.AsNoTracking().Include(p => p.Classes).OrderBy(p => p.ProgramCode).ToListAsync();

        TermDashboardDto Map(AcademicTerm t) => new()
        {
            TermId = t.TermId,
            Name = t.Name,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Status = t.Status is "RestoredIncoming" or "WarningRestored" ? "Incoming" : t.Status,
            IsRestored = t.Status is "RestoredIncoming" or "WarningRestored",
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
            Current = terms.Where(t => t.Status != "Warning" && t.Status != "Abandoned").Select(Map).ToList(),
            Completed = terms.Where(t => t.Status is "Warning" or "Abandoned").OrderByDescending(t => t.EndDate).Select(Map).ToList()
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
    public async Task<IActionResult> Rooms()
    {
        await EnsureFixedRoomsAsync();
        var rooms = await _db.Classrooms.AsNoTracking()
            .Where(r => FixedRoomNames.Contains(r.RoomName))
            .OrderBy(r => r.RoomName)
            .Select(r => new SimpleLookupDto { Id = r.ClassroomId, Name = r.RoomName })
            .ToListAsync();
        return Json(rooms);
    }

    [HttpGet]
    public async Task<IActionResult> RoomAvailability(string termId, DateTime date, string startTime, string endTime, string? excludeTimetableId = null)
    {
        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end) || start >= end)
            return BadRequest("Invalid lesson time.");
        var term = await _db.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.TermId == termId);
        if (term is null) return NotFound("Academic term not found.");
        if (date.Date < term.StartDate.Date || date.Date > term.EndDate.Date)
            return BadRequest("The selected date is outside the semester.");

        return Json(await GetRoomAvailabilityAsync(termId, date.Date, start, end, excludeTimetableId));
    }

    [HttpGet]
    public async Task<IActionResult> TemplateRoomAvailability(string termId, DayOfWeek dayOfWeek, string startTime, string endTime, string? excludeTimetableId = null)
    {
        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end) || start >= end)
            return BadRequest("Invalid lesson time.");
        await EnsureFixedRoomsAsync();
        var rooms = await _db.Classrooms.AsNoTracking().Where(r => FixedRoomNames.Contains(r.RoomName)).OrderBy(r => r.RoomName).ToListAsync();
        var slots = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == termId && t.DayOfWeek == dayOfWeek && t.TimetableId != excludeTimetableId && t.StartTime < end && start < t.EndTime)
            .ToListAsync();
        var result = rooms.Select(room =>
        {
            var used = slots.FirstOrDefault(t => t.ClassroomId == room.ClassroomId);
            return new RoomAvailabilityDto
            {
                Id = room.ClassroomId,
                Name = room.RoomName,
                Available = used == null,
                Reason = used == null ? null : $"Classroom conflict: {room.RoomName} is already occupied from {used.StartTime:hh\\:mm} to {used.EndTime:hh\\:mm}."
            };
        }).ToList();
        return Json(result);
    }

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
        await EnsureFixedRoomsAsync();

        var entries = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == termId && t.ClassId == classId && t.ClassType == type)
            .Include(t => t.Course).Include(t => t.ClassGroup).Include(t => t.Classroom).Include(t => t.Teacher)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime).ToListAsync();

        DateTime? weekStart = weekDate?.Date;
        var events = weekStart.HasValue
            ? await _db.AcademicCalendarEvents.AsNoTracking()
                .Where(e => e.AffectsClasses && e.EventDate >= weekStart.Value && e.EventDate < weekStart.Value.AddDays(7)).ToListAsync()
            : new List<AcademicCalendarEvent>();

        var entryIds = entries.Select(x => x.TimetableId).ToList();
        var exceptions = weekStart.HasValue
            ? await _db.TimetableExceptions.AsNoTracking()
                .Where(e => entryIds.Contains(e.TimetableId) && e.OccurrenceDate >= weekStart.Value && e.OccurrenceDate < weekStart.Value.AddDays(7)).ToListAsync()
            : new List<TimetableException>();

        var weekReplacements = weekStart.HasValue
            ? await _db.ReplacementSessions.AsNoTracking()
                .Include(r => r.SourceTimetable)!.ThenInclude(t => t!.Course)
                .Include(r => r.SourceTimetable)!.ThenInclude(t => t!.Teacher)
                .Where(r => r.Status != "Cancelled" && r.TargetClassType == type && r.SourceTimetable != null &&
                            r.SourceTimetable.TermId == termId && r.SourceTimetable.ClassId == classId &&
                            r.ReplacementDate >= weekStart.Value && r.ReplacementDate < weekStart.Value.AddDays(7))
                .ToListAsync()
            : new List<ReplacementSession>();

        var allTermEntries = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == termId && t.ClassType == type)
            .ToListAsync();
        var roomMap = await _db.Classrooms.AsNoTracking()
            .Where(r => FixedRoomNames.Contains(r.RoomName))
            .ToDictionaryAsync(r => r.ClassroomId, r => r.RoomName);

        var result = new List<TimetableSlotDto>();
        foreach (var t in entries)
        {
            string status = "Normal";
            string? reason = null;
            bool updated = false;
            string? effectiveRoomId = t.ClassroomId;
            var effectiveStart = t.StartTime;
            var effectiveEnd = t.EndTime;
            DateTime? occurrence = null;
            TimetableException? ex = null;
            bool hasReplacement = false;

            if (weekStart.HasValue)
            {
                occurrence = OccurrenceDate(weekStart.Value, t.DayOfWeek);
                ex = exceptions.FirstOrDefault(e => e.TimetableId == t.TimetableId && e.OccurrenceDate.Date == occurrence.Value.Date);
                if (ex != null)
                {
                    effectiveRoomId = ex.OverrideClassroomId ?? effectiveRoomId;
                    effectiveStart = ex.OverrideStartTime ?? effectiveStart;
                    effectiveEnd = ex.OverrideEndTime ?? effectiveEnd;
                    updated = ex.ResolvedAt.HasValue || ex.Decision is "Keep" or "Cancel";
                }

                hasReplacement = ex?.Decision == "Cancel" && await _db.ReplacementSessions.AsNoTracking()
                    .AnyAsync(r => r.SourceTimetableId == t.TimetableId && r.SourceOccurrenceDate == occurrence.Value.Date && r.Status != "Cancelled");

                if (ex?.Decision == "Cancel")
                {
                    status = "Cancelled";
                    reason = ex.Reason == "Holiday" ? "Holiday - Admin Cancelled" : (ex.Note ?? "Admin Cancelled");
                }
                else
                {
                    var ev = events.FirstOrDefault(e => e.EventDate.Date == occurrence.Value.Date);
                    if (ev != null && ex?.Decision != "Keep")
                    {
                        status = "Attention";
                        reason = $"{ev.Name} - Pending Admin Review";
                    }

                    if (status != "Cancelled")
                    {
                        var occurrenceConflict = await GetOccurrenceConflictReasonAsync(t, occurrence.Value, effectiveStart, effectiveEnd);
                        if (!string.IsNullOrWhiteSpace(occurrenceConflict))
                        {
                            status = "Attention";
                            reason = occurrenceConflict;
                        }

                        var availability = await GetRoomAvailabilityAsync(termId, occurrence.Value, effectiveStart, effectiveEnd, t.TimetableId);
                        if (string.IsNullOrWhiteSpace(effectiveRoomId))
                        {
                            status = "Attention";
                            reason = availability.Any(r => r.Available)
                                ? "Classroom required - select one of the 6 available classrooms"
                                : "No classroom available - all 6 classrooms are occupied";
                        }
                        else
                        {
                            var selected = availability.FirstOrDefault(r => r.Id == effectiveRoomId);
                            if (selected is null)
                            {
                                status = "Attention";
                                reason = "Invalid classroom - choose one of the 6 fixed classrooms";
                            }
                            else if (!selected.Available)
                            {
                                status = "Attention";
                                reason = selected.Reason ?? "Classroom conflict - this room is already occupied";
                            }
                        }
                    }
                }
            }
            else
            {
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
                if (string.IsNullOrWhiteSpace(t.ClassroomId))
                {
                    var fixedIds = roomMap.Keys.ToHashSet();
                    var occupied = allTermEntries
                        .Where(other => other.TimetableId != t.TimetableId && other.DayOfWeek == t.DayOfWeek && other.StartTime < t.EndTime && t.StartTime < other.EndTime && !string.IsNullOrWhiteSpace(other.ClassroomId) && fixedIds.Contains(other.ClassroomId!))
                        .Select(other => other.ClassroomId!)
                        .Distinct()
                        .Count();
                    status = "Attention";
                    reason = occupied >= FixedRoomNames.Length
                        ? "No classroom available - all 6 classrooms are occupied"
                        : "Classroom required - choose one of the 6 fixed classrooms";
                }
            }

            if (weekStart.HasValue && occurrence.HasValue && status == "Attention")
                await EnsurePendingAuditAsync(t, occurrence.Value, reason ?? "Attention required");

            result.Add(new TimetableSlotDto
            {
                TermId = t.TermId,
                TimetableId = t.TimetableId,
                TeacherId = t.TeacherId,
                TeacherName = t.Teacher.Username,
                CourseId = t.CourseId,
                CourseName = t.Course?.CourseName ?? "",
                ClassId = t.ClassId,
                ClassName = t.ClassGroup?.ClassName ?? "",
                ClassroomId = effectiveRoomId,
                RoomName = effectiveRoomId != null && roomMap.TryGetValue(effectiveRoomId, out var roomName) ? roomName : null,
                DayOfWeek = t.DayOfWeek,
                StartTime = effectiveStart.ToString(@"hh\:mm"),
                EndTime = effectiveEnd.ToString(@"hh\:mm"),
                ClassType = t.ClassType,
                Status = status,
                StatusReason = reason,
                IsUpdated = updated && status == "Normal",
                HasReplacement = hasReplacement
            });
        }

        if (weekStart.HasValue)
        {
            foreach (var rep in weekReplacements)
            {
                var source = rep.SourceTimetable;
                if (source is null) continue;
                result.Add(new TimetableSlotDto
                {
                    TermId = source.TermId,
                    TimetableId = source.TimetableId,
                    TeacherId = rep.TeacherId,
                    TeacherName = source.Teacher?.Username ?? rep.TeacherId,
                    CourseId = source.CourseId,
                    CourseName = source.Course?.CourseName ?? source.CourseId,
                    ClassId = source.ClassId,
                    ClassName = source.ClassGroup?.ClassName ?? string.Empty,
                    ClassroomId = rep.ClassroomId,
                    RoomName = rep.ClassroomId != null && roomMap.TryGetValue(rep.ClassroomId, out var replacementRoomName) ? replacementRoomName : null,
                    DayOfWeek = rep.ReplacementDate.DayOfWeek,
                    StartTime = rep.StartTime.ToString(@"hh\:mm"),
                    EndTime = rep.EndTime.ToString(@"hh\:mm"),
                    ClassType = rep.TargetClassType,
                    Status = "Normal",
                    StatusReason = "Replacement class",
                    IsUpdated = true,
                    IsReplacement = true,
                    ReplacementId = rep.ReplacementId,
                    SourceOccurrenceDate = rep.SourceOccurrenceDate
                });
            }
        }

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] TimetableRequest r)
    {
        var term = await _db.AcademicTerms.FirstOrDefaultAsync(t => t.TermId == r.TermId);
        if (term is null) return BadRequest("Please select an academic term.");
        if (!string.Equals(r.Status, "Normal", StringComparison.OrdinalIgnoreCase))
            return BadRequest(string.IsNullOrWhiteSpace(r.TimetableId)
                ? "A new lesson cannot be created as Cancelled. Create the lesson first, then select a specific Week and lesson date to cancel it."
                : "Status cannot be changed from the Weekly template. Please select the Week and specific lesson date you want to change.");
        if (term.Status is "Warning" or "Abandoned") return BadRequest("History semesters are read-only.");

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
        if (term?.Status is "Warning" or "Abandoned") return BadRequest("History semesters are read-only.");
        _db.Timetables.Remove(e);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SaveOccurrence([FromBody] OccurrenceUpdateRequest r)
    {
        if (r.Status is not ("Normal" or "Cancelled")) return BadRequest("Status must be Normal or Cancelled.");
        if (!TimeSpan.TryParse(r.StartTime, out var start) || !TimeSpan.TryParse(r.EndTime, out var end))
            return BadRequest("Please enter a valid start and end time.");
        if (start >= end) return BadRequest("End time must be later than start time.");
        if (start < TimeSpan.FromHours(8) || end > TimeSpan.FromHours(18))
            return BadRequest("Lesson time must be between 08:00 AM and 06:00 PM.");

        var t = await _db.Timetables
            .Include(x => x.Course).Include(x => x.ClassGroup).Include(x => x.Classroom).Include(x => x.Teacher)
            .FirstOrDefaultAsync(x => x.TimetableId == r.TimetableId);
        if (t is null) return NotFound("Lesson not found.");
        var term = await _db.AcademicTerms.FindAsync(t.TermId);
        if (term is null) return BadRequest("Academic term not found.");
        if (term.Status is "Warning" or "Abandoned") return BadRequest("History semesters are read-only.");
        if (r.Date == default) return BadRequest("Please select the Week and specific lesson date you want to change. Status cannot be changed from the Weekly template.");
        if (r.Date.Date < term.StartDate.Date || r.Date.Date > term.EndDate.Date) return BadRequest("The selected date is outside the semester.");
        if (r.Date.DayOfWeek != t.DayOfWeek) return BadRequest("The selected date does not match the timetable day.");

        if (r.Status == "Normal")
        {
            var hasScheduledReplacement = await _db.ReplacementSessions.AsNoTracking()
                .AnyAsync(x => x.SourceTimetableId == t.TimetableId
                    && x.SourceOccurrenceDate == r.Date.Date
                    && x.Status != "Cancelled");
            if (hasScheduledReplacement)
                return BadRequest("This cancelled lesson already has a scheduled replacement class. Its status cannot be changed back to Normal. Cancel or remove the replacement first if the original lesson must be restored.");
        }

        var conflict = await GetOccurrenceConflictReasonAsync(t, r.Date.Date, start, end);
        if (r.Status == "Normal" && conflict is not null) return BadRequest(conflict + ". Please choose another time.");

        await EnsureFixedRoomsAsync();
        var rooms = await GetRoomAvailabilityAsync(t.TermId!, r.Date.Date, start, end, t.TimetableId);
        if (r.Status == "Normal")
        {
            if (string.IsNullOrWhiteSpace(r.ClassroomId))
            {
                if (rooms.Any(x => x.Available))
                    return BadRequest("Please select one of the 6 fixed classrooms. Available rooms are: " + string.Join(", ", rooms.Where(x => x.Available).Select(x => x.Name)) + ".");
                await EnsurePendingAuditAsync(t, r.Date.Date, "No classroom available - all 6 fixed classrooms are occupied");
                await _db.SaveChangesAsync();
                return BadRequest("No classroom is available for this lesson. All 6 fixed classrooms are occupied, so this timetable cell remains yellow (Attention) and requires Admin action.");
            }
            var selectedRoom = rooms.FirstOrDefault(x => x.Id == r.ClassroomId);
            if (selectedRoom is null) return BadRequest("Invalid classroom. Please choose one of the 6 fixed classrooms.");
            if (!selectedRoom.Available) return BadRequest(selectedRoom.Reason ?? "Classroom conflict: this classroom is already occupied at the selected time.");
        }

        var holiday = await _db.AcademicCalendarEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.AffectsClasses && e.EventDate.Date == r.Date.Date);
        var ex = await _db.TimetableExceptions
            .FirstOrDefaultAsync(x => x.TimetableId == r.TimetableId && x.OccurrenceDate.Date == r.Date.Date);
        var beforeRoomId = ex?.OverrideClassroomId ?? t.ClassroomId;
        var beforeStart = ex?.OverrideStartTime ?? t.StartTime;
        var beforeEnd = ex?.OverrideEndTime ?? t.EndTime;
        var beforeRoom = await RoomNameAsync(beforeRoomId);
        var beforeStatus = ex?.Decision == "Cancel" ? "Cancelled" : "Normal";
        var before = $"{r.Date:dd MMM yyyy} | {beforeStart:hh\\:mm}-{beforeEnd:hh\\:mm} | {beforeRoom ?? "No classroom"} | {beforeStatus}";

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
        if (r.Status == "Normal")
        {
            ex.OverrideClassroomId = r.ClassroomId;
            ex.OverrideStartTime = start;
            ex.OverrideEndTime = end;
        }
        ex.ResolvedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ex.ResolvedAt = DateTime.Now;

        var sessions = await _db.ClassSessions.Where(x => x.TimetableId == r.TimetableId && x.SessionDate == r.Date.Date).ToListAsync();
        foreach (var session in sessions)
        {
            session.Status = r.Status == "Cancelled" ? "Cancelled" : "Scheduled";
            if (r.Status == "Normal") { session.StartTime = start; session.EndTime = end; }
            session.Reason = r.Status == "Cancelled" ? (holiday is null ? "Admin Cancelled" : "Holiday - Admin Cancelled") : "Updated by Admin";
        }

        var afterRoom = r.Status == "Cancelled" ? beforeRoom : await RoomNameAsync(r.ClassroomId);
        var afterStart = r.Status == "Cancelled" ? beforeStart : start;
        var afterEnd = r.Status == "Cancelled" ? beforeEnd : end;
        var after = $"{r.Date:dd MMM yyyy} | {afterStart:hh\\:mm}-{afterEnd:hh\\:mm} | {afterRoom ?? "No classroom"} | {r.Status}";
        await ResolveAuditAsync(t, r.Date.Date, before, after, r.Status == "Cancelled" ? "Cancelled by Admin" : "Lesson updated by Admin");
        await _db.SaveChangesAsync();

        await NotifyAffectedUsersAsync(t, r.Date.Date, before, after, r.Status);
        if (r.Status == "Cancelled")
            await EnsureReplacementRequiredAuditAsync(t, r.Date.Date, before);
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
        if (term.Status is "Warning" or "Abandoned") return BadRequest("History semesters are read-only.");
        if (r.Date.Date < term.StartDate.Date || r.Date.Date > term.EndDate.Date) return BadRequest("The selected date is outside the semester.");
        if (r.Date.DayOfWeek != t.DayOfWeek) return BadRequest("The selected date does not match the timetable day.");

        if (r.Status == "Normal")
        {
            var hasScheduledReplacement = await _db.ReplacementSessions.AsNoTracking()
                .AnyAsync(x => x.SourceTimetableId == t.TimetableId
                    && x.SourceOccurrenceDate == r.Date.Date
                    && x.Status != "Cancelled");
            if (hasScheduledReplacement)
                return BadRequest("This cancelled lesson already has a scheduled replacement class. Its status cannot be changed back to Normal. Cancel or remove the replacement first if the original lesson must be restored.");
        }

        var holiday = await _db.AcademicCalendarEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.AffectsClasses && e.EventDate.Date == r.Date.Date);
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
        {
            await AddNotification("ReplacementRequired", $"Cancelled lesson {r.TimetableId} on {r.Date:dd MMM yyyy} requires review for replacement.", t.TermId, r.TimetableId);
            var room = await RoomNameAsync(t.ClassroomId);
            var before = $"{r.Date:dd MMM yyyy} | {t.StartTime:hh\\:mm}-{t.EndTime:hh\\:mm} | {room ?? "No classroom"} | Cancelled";
            await EnsureReplacementRequiredAuditAsync(t, r.Date.Date, before);
        }
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

    [HttpGet]
    public async Task<IActionResult> ReplacementRequired(string termId, string classId, DateTime weekDate)
    {
        var from = weekDate.Date;
        var to = from.AddDays(7);
        var rows = await _db.TimetableExceptions.AsNoTracking()
            .Include(e => e.Timetable)!.ThenInclude(t => t!.Course)
            .Include(e => e.Timetable)!.ThenInclude(t => t!.Teacher)
            .Include(e => e.Timetable)!.ThenInclude(t => t!.Classroom)
            .Where(e => e.Decision == "Cancel" && e.OccurrenceDate >= from && e.OccurrenceDate < to &&
                        e.Timetable != null && e.Timetable.TermId == termId && e.Timetable.ClassId == classId)
            .OrderBy(e => e.OccurrenceDate).ThenBy(e => e.Timetable!.StartTime)
            .ToListAsync();

        var result = new List<ReplacementRequiredDto>();
        foreach (var ex in rows)
        {
            var t = ex.Timetable!;
            var scheduled = await _db.ReplacementSessions.AsNoTracking()
                .FirstOrDefaultAsync(r => r.SourceTimetableId == t.TimetableId && r.SourceOccurrenceDate == ex.OccurrenceDate.Date && r.Status != "Cancelled");

            // This endpoint is a work queue, not history. Once a replacement has been
            // scheduled, the cancelled lesson leaves this panel and the replacement
            // is shown directly in the Tuition timetable grid for its new date.
            if (scheduled != null)
                continue;

            result.Add(new ReplacementRequiredDto
            {
                TimetableId = t.TimetableId,
                OccurrenceDate = ex.OccurrenceDate.Date,
                CourseName = t.Course?.CourseName ?? t.CourseId,
                TeacherName = t.Teacher?.Username ?? t.TeacherId,
                TeacherId = t.TeacherId,
                ClassroomId = ex.OverrideClassroomId ?? t.ClassroomId,
                RoomName = ex.OverrideClassroomId != null ? await RoomNameAsync(ex.OverrideClassroomId) : t.Classroom?.RoomName,
                StartTime = (ex.OverrideStartTime ?? t.StartTime).ToString(@"hh\:mm"),
                EndTime = (ex.OverrideEndTime ?? t.EndTime).ToString(@"hh\:mm"),
                HasReplacement = scheduled != null,
                ReplacementDate = scheduled?.ReplacementDate,
                ReplacementStartTime = scheduled?.StartTime.ToString(@"hh\:mm"),
                ReplacementEndTime = scheduled?.EndTime.ToString(@"hh\:mm"),
                ReplacementRoomName = scheduled?.ClassroomId == null ? null : await RoomNameAsync(scheduled.ClassroomId)
            });
        }
        return Json(result.OrderBy(x => x.HasReplacement).ThenBy(x => x.OccurrenceDate).ThenBy(x => x.StartTime));
    }

    [HttpGet]
    public async Task<IActionResult> ReplacementRoomAvailability(string termId, DateTime date, string startTime, string endTime)
    {
        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end) || start >= end)
            return BadRequest("Invalid replacement time.");
        return Json(await GetRoomAvailabilityAsync(termId, date.Date, start, end, null));
    }

    [HttpPost]
    public async Task<IActionResult> CreateReplacement([FromBody] ReplacementRequest r)
    {
        var source = await _db.Timetables
            .Include(t => t.Course).Include(t => t.Teacher).Include(t => t.Classroom)
            .FirstOrDefaultAsync(t => t.TimetableId == r.SourceTimetableId);
        if (source is null) return NotFound("Original lesson not found.");
        var term = await _db.AcademicTerms.FindAsync(source.TermId);
        if (term is null) return BadRequest("Academic term not found.");
        if (term.Status is "Warning" or "Abandoned") return BadRequest("History semesters are read-only.");

        var cancelled = await _db.TimetableExceptions.AsNoTracking().FirstOrDefaultAsync(e =>
            e.TimetableId == source.TimetableId && e.OccurrenceDate == r.SourceOccurrenceDate.Date && e.Decision == "Cancel");
        if (cancelled is null)
            return BadRequest("Replacement can only be scheduled for a specific cancelled lesson.");
        if (await _db.ReplacementSessions.AnyAsync(x => x.SourceTimetableId == source.TimetableId && x.SourceOccurrenceDate == r.SourceOccurrenceDate.Date && x.Status != "Cancelled"))
            return BadRequest("A replacement has already been scheduled for this cancelled lesson.");

        if (r.Date.Date < r.SourceOccurrenceDate.Date)
            return BadRequest("Replacement date cannot be earlier than the cancelled lesson date.");
        if (r.Date.Date > term.EndDate.Date)
            return BadRequest("Replacement date cannot be after the semester end date.");
        if (r.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            return BadRequest("Replacement classes must be scheduled in the Tuition timetable on Saturday or Sunday.");
        if (!TimeSpan.TryParse(r.StartTime, out var s) || !TimeSpan.TryParse(r.EndTime, out var e) || s >= e)
            return BadRequest("Invalid replacement time.");
        if (s < TimeSpan.FromHours(8) || e > TimeSpan.FromHours(18))
            return BadRequest("Replacement time must be between 08:00 AM and 06:00 PM.");

        var originalStart = cancelled.OverrideStartTime ?? source.StartTime;
        var originalEnd = cancelled.OverrideEndTime ?? source.EndTime;
        if (e - s != originalEnd - originalStart)
            return BadRequest($"Replacement must keep the original lesson duration of {(originalEnd - originalStart).TotalHours:0.#} hour(s).");

        await EnsureFixedRoomsAsync();
        if (string.IsNullOrWhiteSpace(r.ClassroomId)) return BadRequest("Please select one of the 6 fixed classrooms.");
        var rooms = await GetRoomAvailabilityAsync(source.TermId!, r.Date.Date, s, e, null);
        var selectedRoom = rooms.FirstOrDefault(x => x.Id == r.ClassroomId);
        if (selectedRoom is null) return BadRequest("Invalid classroom. Please choose one of the 6 fixed classrooms.");
        if (!selectedRoom.Available) return BadRequest(selectedRoom.Reason ?? "Classroom conflict at the selected replacement time.");

        // Hard conflicts still apply. Only normal Tuition daily/consecutive-hour quotas are bypassed for replacements.
        var dayTemplates = await _db.Timetables.AsNoTracking().Where(t => t.TermId == source.TermId && t.DayOfWeek == r.Date.DayOfWeek).ToListAsync();
        var dayIds = dayTemplates.Select(t => t.TimetableId).ToList();
        var dayExceptions = await _db.TimetableExceptions.AsNoTracking().Where(x => dayIds.Contains(x.TimetableId) && x.OccurrenceDate == r.Date.Date).ToListAsync();
        foreach (var other in dayTemplates)
        {
            var ox = dayExceptions.FirstOrDefault(x => x.TimetableId == other.TimetableId);
            if (ox?.Decision == "Cancel") continue;
            var os = ox?.OverrideStartTime ?? other.StartTime;
            var oe = ox?.OverrideEndTime ?? other.EndTime;
            if (!(os < e && s < oe)) continue;
            var roomId = ox?.OverrideClassroomId ?? other.ClassroomId;
            if (other.TeacherId == source.TeacherId) return BadRequest("Teacher conflict: this teacher already has a lesson at the selected replacement time.");
            if (other.ClassId == source.ClassId) return BadRequest("Class conflict: this class already has a lesson at the selected replacement time.");
            if (roomId == r.ClassroomId) return BadRequest("Classroom conflict: this classroom is already occupied at the selected replacement time.");
        }

        var otherReplacements = await _db.ReplacementSessions.AsNoTracking().Include(x => x.SourceTimetable)
            .Where(x => x.ReplacementDate == r.Date.Date && x.Status != "Cancelled" && x.StartTime < e && s < x.EndTime).ToListAsync();
        if (otherReplacements.Any(x => x.TeacherId == source.TeacherId)) return BadRequest("Teacher conflict: this teacher already has another replacement at this time.");
        if (otherReplacements.Any(x => x.SourceTimetable != null && x.SourceTimetable.ClassId == source.ClassId)) return BadRequest("Class conflict: this class already has another replacement at this time.");
        if (otherReplacements.Any(x => x.ClassroomId == r.ClassroomId)) return BadRequest("Classroom conflict: this classroom is already used by another replacement at this time.");

        var rep = new ReplacementSession
        {
            ReplacementId = await FormattedIdGenerator.NextAsync(_db.ReplacementSessions, x => x.ReplacementId, "RPL"),
            SourceTimetableId = source.TimetableId,
            SourceOccurrenceDate = r.SourceOccurrenceDate.Date,
            ReplacementDate = r.Date.Date,
            StartTime = s,
            EndTime = e,
            TargetClassType = ClassType.Tuition,
            TeacherId = source.TeacherId,
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
            Reason = $"Make-up for {r.SourceOccurrenceDate:dd MMM yyyy}"
        };
        _db.ClassSessions.Add(session);

        var originalRoom = await RoomNameAsync(cancelled.OverrideClassroomId ?? source.ClassroomId);
        var replacementRoom = await RoomNameAsync(r.ClassroomId);
        var before = $"Cancelled {r.SourceOccurrenceDate:dd MMM yyyy} | {originalStart:hh\\:mm}-{originalEnd:hh\\:mm} | {originalRoom ?? "No classroom"}";
        var after = $"Replacement {r.Date:dd MMM yyyy} | {s:hh\\:mm}-{e:hh\\:mm} | {replacementRoom ?? "No classroom"}";
        await ResolveReplacementAuditAsync(source, r.SourceOccurrenceDate.Date, before, after);
        await _db.SaveChangesAsync();
        await NotifyReplacementScheduledAsync(source, r.SourceOccurrenceDate.Date, r.Date.Date, s, e, replacementRoom);

        // Tell the client exactly which academic Week owns the new replacement date.
        // Do not make the browser guess the Week from a 7-day calculation because
        // semester weeks can be partial at the start/end of a term.
        var replacementWeek = await _db.AcademicWeeks.AsNoTracking()
            .Where(w => w.TermId == source.TermId && w.StartDate <= r.Date.Date && w.EndDate >= r.Date.Date)
            .OrderBy(w => w.WeekNumber)
            .FirstOrDefaultAsync();

        return Json(new
        {
            rep.ReplacementId,
            session.SessionId,
            ReplacementDate = r.Date.Date,
            ReplacementWeekStart = replacementWeek?.StartDate.Date,
            ReplacementWeekNumber = replacementWeek?.WeekNumber
        });
    }

    [HttpGet]
    public async Task<IActionResult> Audit(string status = "all")
    {
        var query = _db.TimetableAudits.AsNoTracking().AsQueryable();
        if (status.Equals("pending", StringComparison.OrdinalIgnoreCase)) query = query.Where(a => a.Status == "Pending");
        else if (status.Equals("resolved", StringComparison.OrdinalIgnoreCase)) query = query.Where(a => a.Status == "Resolved");

        var rows = await query.OrderBy(a => a.Status == "Resolved").ThenByDescending(a => a.CreatedAt).Take(200).ToListAsync();
        return Json(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Notifications() => Json(await _db.TimetableNotifications.AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> CreateTerm([FromBody] CreateAcademicTermRequest request)
    {
        var today = DateTime.Today;
        await SyncSemesterStatusesAsync(today);

        var name = (request.Name ?? string.Empty).Trim();
        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        var validationError = await ValidateSemesterDatesAsync(null, name, startDate, endDate, today);
        if (validationError is not null) return BadRequest(validationError);

        var currentTerms = await _db.AcademicTerms
            .Where(t => t.Status != "Warning" && t.Status != "Abandoned")
            .OrderBy(t => t.StartDate)
            .ToListAsync();
        if (currentTerms.Count >= 2)
            return BadRequest("Only two semesters can be kept on the timetable page at the same time. Move one semester to History first.");

        var activeExists = currentTerms.Any(t => t.Status == "Active");
        var incomingExists = currentTerms.Any(t => t.Status is "Incoming" or "RestoredIncoming" or "WarningRestored");

        if (startDate > today && incomingExists)
            return BadRequest("Only one Incoming semester can exist at a time. Edit or move the existing Incoming semester to History first.");

        if (startDate == today && !activeExists && !request.ForceActivate)
        {
            return Conflict(new
            {
                code = "FORCE_ACTIVE_CONFIRMATION_REQUIRED",
                message = "This semester starts today, so it must become Active immediately. Confirm to create and force this semester to Active."
            });
        }

        if (startDate == today && activeExists)
            return BadRequest("An Active semester already exists. Two Active semesters are not allowed.");

        var term = new AcademicTerm
        {
            TermId = await FormattedIdGenerator.NextAsync(_db.AcademicTerms, x => x.TermId, "TRM"),
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            Status = startDate == today && request.ForceActivate ? "Active" : "Incoming"
        };

        _db.AcademicTerms.Add(term);
        await RebuildWeeksAsync(term);
        await _db.SaveChangesAsync();

        return Json(new
        {
            termId = term.TermId,
            name = term.Name,
            startDate = term.StartDate,
            endDate = term.EndDate,
            status = term.Status
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateTermDates([FromBody] UpdateAcademicTermDatesRequest request)
    {
        var term = await _db.AcademicTerms.FirstOrDefaultAsync(t => t.TermId == request.TermId);
        if (term is null) return NotFound("Semester not found.");
        if (term.Status != "Incoming")
            return BadRequest("Only an Incoming semester can change its start and end dates. Active semester dates are locked.");

        var today = DateTime.Today;
        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;
        var validationError = await ValidateSemesterDatesAsync(term.TermId, term.Name, startDate, endDate, today, checkName: false);
        if (validationError is not null) return BadRequest(validationError);

        term.StartDate = startDate;
        term.EndDate = endDate;
        await RebuildWeeksAsync(term);
        await _db.SaveChangesAsync();

        return Json(new
        {
            termId = term.TermId,
            name = term.Name,
            startDate = term.StartDate,
            endDate = term.EndDate,
            status = term.Status
        });
    }

    [HttpPost]
    public async Task<IActionResult> CompleteTerm(string termId)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return NotFound();
        term.Status = "Warning";
        await _db.SaveChangesAsync();
        await SyncSemesterStatusesAsync(DateTime.Today);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> RestoreTerm(string termId)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return NotFound("Semester not found.");
        if (term.Status != "Warning") return BadRequest("Only a semester in History can be restored.");

        var currentTerms = await _db.AcademicTerms
            .Where(t => t.Status != "Warning" && t.Status != "Abandoned")
            .ToListAsync();

        if (currentTerms.Count >= 2)
            return BadRequest("The timetable page already contains two semesters. Move one semester to History before restoring another.");

        if (currentTerms.Any(t => t.Status is "Incoming" or "RestoredIncoming" or "WarningRestored"))
            return BadRequest("An Incoming semester already exists. Move it to History or activate it before restoring another semester as Incoming.");

        // A restored semester returns to the timetable page as Incoming.
        // It stays unpublished until the admin explicitly activates it.
        term.Status = "RestoredIncoming";
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> ActivateRestoredTerm(string termId)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return NotFound("Semester not found.");
        if (term.Status is not ("RestoredIncoming" or "WarningRestored"))
            return BadRequest("Only a restored Incoming semester can be activated from this control.");

        var today = DateTime.Today;
        if (today < term.StartDate.Date || today > term.EndDate.Date)
            return BadRequest("This semester cannot become Active today because today's date is outside its semester dates. Edit the Incoming semester dates first.");

        var anotherActive = await _db.AcademicTerms
            .AnyAsync(t => t.TermId != termId && t.Status == "Active");
        if (anotherActive)
            return BadRequest("Another Active semester already exists. Only one Active semester is allowed at a time.");

        term.Status = "Active";
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> AbandonTerm(string termId)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return NotFound("Semester not found.");
        if (term.Status != "Warning") return BadRequest("Only a semester in Completed / History can be abandoned.");

        term.Status = "Abandoned";
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> UndoAbandonTerm(string termId)
    {
        var term = await _db.AcademicTerms.FindAsync(termId);
        if (term is null) return NotFound("Semester not found.");
        if (term.Status != "Abandoned") return BadRequest("Only an abandoned semester can be restored to History.");

        var conflict = await _db.AcademicTerms.AsNoTracking()
            .Where(t => t.TermId != termId && t.Status != "Abandoned" &&
                        t.StartDate <= term.EndDate && term.StartDate <= t.EndDate)
            .OrderBy(t => t.StartDate)
            .FirstOrDefaultAsync();

        if (conflict is not null)
        {
            return BadRequest($"Cannot undo Abandon for {term.Name}. Its dates {term.StartDate:dd MMM yyyy} - {term.EndDate:dd MMM yyyy} overlap with {conflict.Name} ({conflict.StartDate:dd MMM yyyy} - {conflict.EndDate:dd MMM yyyy}). Change or abandon the conflicting semester first.");
        }

        term.Status = "Warning";
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
        e.Name = (e.Name ?? string.Empty).Trim();
        e.EventType = (e.EventType ?? "PublicHoliday").Trim();
        e.EventDate = e.EventDate.Date;
        e.TermId = null;

        if (e.EventDate < DateTime.Today) return BadRequest("Past dates cannot be added as new holidays.");
        if (e.Name.Length < 2 || e.Name.Length > 150) return BadRequest("Holiday name must be between 2 and 150 characters.");

        var allowedTypes = new[] { "PublicHoliday", "UniversityHoliday", "Closure" };
        if (!allowedTypes.Contains(e.EventType, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Invalid holiday type. Allowed types: PublicHoliday, UniversityHoliday, Closure.");

        e.EventType = allowedTypes.First(x => x.Equals(e.EventType, StringComparison.OrdinalIgnoreCase));

        if (await _db.AcademicCalendarEvents.AnyAsync(x => x.EventDate.Date == e.EventDate && x.Name.ToLower() == e.Name.ToLower()))
            return BadRequest($"{e.Name} on {e.EventDate:dd MMM yyyy} already exists in the Holiday Calendar.");

        e.CalendarEventId = await FormattedIdGenerator.NextAsync(_db.AcademicCalendarEvents, x => x.CalendarEventId, "CAL");
        e.AffectsClasses = true;
        _db.AcademicCalendarEvents.Add(e);
        await _db.SaveChangesAsync();

        var affectedTerms = await _db.AcademicTerms.AsNoTracking()
            .Where(t => t.Status != "Abandoned" && t.StartDate <= e.EventDate && e.EventDate <= t.EndDate)
            .Select(t => t.TermId).ToListAsync();
        foreach (var termId in affectedTerms)
            await AddNotification("HolidayAffected", $"{e.Name} on {e.EventDate:dd MMM yyyy} may affect scheduled classes.", termId, null);

        return Json(e);
    }

    [HttpGet]
    public async Task<IActionResult> Calendar() => Json(await _db.AcademicCalendarEvents.AsNoTracking()
        .OrderBy(e => e.EventDate).ThenBy(e => e.Name).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> LoadMalaysia2026Holidays()
    {
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

        int added = 0, skipped = 0;
        foreach (var h in holidays)
        {
            if (await _db.AcademicCalendarEvents.AnyAsync(x => x.EventDate.Date == h.Date.Date && x.Name.ToLower() == h.Name.ToLower()))
            {
                skipped++;
                continue;
            }

            _db.AcademicCalendarEvents.Add(new AcademicCalendarEvent
            {
                CalendarEventId = await FormattedIdGenerator.NextAsync(_db.AcademicCalendarEvents, x => x.CalendarEventId, "CAL"),
                TermId = null,
                EventDate = h.Date,
                Name = h.Name,
                EventType = "PublicHoliday",
                AffectsClasses = true
            });
            added++;
        }
        await _db.SaveChangesAsync();
        return Json(new { added, skipped });
    }

    [HttpPost]
    public async Task<IActionResult> ImportCalendarEvents(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest("Please choose a non-empty .txt or .csv file.");
        if (file.Length > 1024 * 1024) return BadRequest("Holiday import file must be smaller than 1 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".txt" or ".csv"))
            return BadRequest("Only .txt and .csv files are accepted. CSV must use commas; TXT must use | separators.");

        using var reader = new StreamReader(file.OpenReadStream());
        var rows = new List<(int LineNo, DateTime Date, string Name, string Type)>();
        var errors = new List<string>();
        var allowedTypes = new[] { "PublicHoliday", "UniversityHoliday", "Closure" };
        string? line;
        int lineNo = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNo++;
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            if (lineNo == 1 && line.Contains("date", StringComparison.OrdinalIgnoreCase) && line.Contains("name", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = ext == ".csv" ? line.Split(',', 3) : line.Split('|', 3);
            if (parts.Length != 3)
            {
                errors.Add($"Line {lineNo}: expected exactly 3 fields. Required {(ext == ".csv" ? "CSV" : "TXT")} format: {(ext == ".csv" ? "YYYY-MM-DD,Holiday Name,Type" : "YYYY-MM-DD | Holiday Name | Type")}.");
                continue;
            }

            if (!DateTime.TryParseExact(parts[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add($"Line {lineNo}: invalid date '{parts[0].Trim()}'. Required date format: YYYY-MM-DD.");
                continue;
            }

            var name = parts[1].Trim();
            var typeInput = parts[2].Trim();
            if (name.Length < 2 || name.Length > 150)
            {
                errors.Add($"Line {lineNo}: holiday name must be 2-150 characters.");
                continue;
            }

            var canonicalType = allowedTypes.FirstOrDefault(x => x.Equals(typeInput, StringComparison.OrdinalIgnoreCase));
            if (canonicalType is null)
            {
                errors.Add($"Line {lineNo}: invalid type '{typeInput}'. Allowed: PublicHoliday, UniversityHoliday, Closure.");
                continue;
            }

            if (rows.Any(x => x.Date.Date == date.Date && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Line {lineNo}: duplicate holiday '{name}' on {date:yyyy-MM-dd} inside this file.");
                continue;
            }

            rows.Add((lineNo, date.Date, name, canonicalType));
        }

        foreach (var row in rows)
        {
            if (await _db.AcademicCalendarEvents.AnyAsync(x => x.EventDate.Date == row.Date && x.Name.ToLower() == row.Name.ToLower()))
                errors.Add($"Line {row.LineNo}: '{row.Name}' on {row.Date:yyyy-MM-dd} already exists in the Holiday Calendar.");
        }

        if (errors.Count > 0)
        {
            var example = ext == ".csv"
                ? "Example: 2026-09-16,Malaysia Day,PublicHoliday"
                : "Example: 2026-09-16 | Malaysia Day | PublicHoliday";
            return BadRequest("Import failed. No holidays were added.\n\n" + string.Join("\n", errors.Take(12)) +
                "\n\nAllowed types: PublicHoliday, UniversityHoliday, Closure.\n" + example);
        }

        if (rows.Count == 0) return BadRequest("Import failed. The file contains no holiday data rows.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var row in rows)
            {
                _db.AcademicCalendarEvents.Add(new AcademicCalendarEvent
                {
                    CalendarEventId = await FormattedIdGenerator.NextAsync(_db.AcademicCalendarEvents, x => x.CalendarEventId, "CAL"),
                    TermId = null,
                    EventDate = row.Date,
                    Name = row.Name,
                    EventType = row.Type,
                    AffectsClasses = true
                });
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return Json(new { added = rows.Count });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCalendarEvent(string id)
    {
        var e = await _db.AcademicCalendarEvents.FindAsync(id);
        if (e is null) return NotFound();
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
            .Where(e => e.AffectsClasses && e.EventDate >= from && e.EventDate <= to).ToListAsync();
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

    private async Task EnsureFixedRoomsAsync()
    {
        var existing = await _db.Classrooms.Where(r => FixedRoomNames.Contains(r.RoomName)).Select(r => r.RoomName).ToListAsync();
        foreach (var name in FixedRoomNames.Where(n => !existing.Contains(n)))
        {
            _db.Classrooms.Add(new Classroom
            {
                ClassroomId = await FormattedIdGenerator.NextAsync(_db.Classrooms, x => x.ClassroomId, "ROM"),
                RoomName = name
            });
            await _db.SaveChangesAsync();
        }
    }

    private async Task<List<RoomAvailabilityDto>> GetRoomAvailabilityAsync(string termId, DateTime date, TimeSpan start, TimeSpan end, string? excludeTimetableId)
    {
        await EnsureFixedRoomsAsync();
        var rooms = await _db.Classrooms.AsNoTracking().Where(r => FixedRoomNames.Contains(r.RoomName)).OrderBy(r => r.RoomName).ToListAsync();
        var dayEntries = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == termId && t.DayOfWeek == date.DayOfWeek && t.TimetableId != excludeTimetableId)
            .ToListAsync();
        var ids = dayEntries.Select(t => t.TimetableId).ToList();
        var exceptions = await _db.TimetableExceptions.AsNoTracking()
            .Where(x => ids.Contains(x.TimetableId) && x.OccurrenceDate == date.Date).ToListAsync();
        var replacements = await _db.ReplacementSessions.AsNoTracking()
            .Where(r => r.ReplacementDate == date.Date && r.Status != "Cancelled").ToListAsync();

        var result = new List<RoomAvailabilityDto>();
        foreach (var room in rooms)
        {
            bool occupied = false;
            string? reason = null;
            foreach (var t in dayEntries)
            {
                var ex = exceptions.FirstOrDefault(x => x.TimetableId == t.TimetableId);
                if (ex?.Decision == "Cancel") continue;
                var roomId = ex?.OverrideClassroomId ?? t.ClassroomId;
                var s = ex?.OverrideStartTime ?? t.StartTime;
                var e = ex?.OverrideEndTime ?? t.EndTime;
                if (roomId == room.ClassroomId && s < end && start < e)
                {
                    occupied = true;
                    reason = $"Classroom conflict: {room.RoomName} is already occupied from {s:hh\\:mm} to {e:hh\\:mm}.";
                    break;
                }
            }
            if (!occupied)
            {
                var rep = replacements.FirstOrDefault(r => r.ClassroomId == room.ClassroomId && r.StartTime < end && start < r.EndTime);
                if (rep != null)
                {
                    occupied = true;
                    reason = $"Classroom conflict: {room.RoomName} is reserved for a replacement class from {rep.StartTime:hh\\:mm} to {rep.EndTime:hh\\:mm}.";
                }
            }
            result.Add(new RoomAvailabilityDto { Id = room.ClassroomId, Name = room.RoomName, Available = !occupied, Reason = reason });
        }
        return result;
    }

    private async Task<string?> GetOccurrenceConflictReasonAsync(Timetable source, DateTime date, TimeSpan start, TimeSpan end)
    {
        var others = await _db.Timetables.AsNoTracking()
            .Where(t => t.TermId == source.TermId && t.DayOfWeek == date.DayOfWeek && t.TimetableId != source.TimetableId)
            .ToListAsync();
        var ids = others.Select(x => x.TimetableId).ToList();
        var exceptions = await _db.TimetableExceptions.AsNoTracking()
            .Where(x => ids.Contains(x.TimetableId) && x.OccurrenceDate == date.Date).ToListAsync();
        foreach (var other in others)
        {
            var ex = exceptions.FirstOrDefault(x => x.TimetableId == other.TimetableId);
            if (ex?.Decision == "Cancel") continue;
            var os = ex?.OverrideStartTime ?? other.StartTime;
            var oe = ex?.OverrideEndTime ?? other.EndTime;
            if (!(os < end && start < oe)) continue;
            if (other.TeacherId == source.TeacherId)
                return $"Teacher conflict - this teacher is already assigned from {os:hh\\:mm} to {oe:hh\\:mm}";
            if (other.ClassId == source.ClassId)
                return $"Class/time conflict - this class already has another lesson from {os:hh\\:mm} to {oe:hh\\:mm}";
        }
        var replacements = await _db.ReplacementSessions.AsNoTracking().Include(r => r.SourceTimetable)
            .Where(r => r.ReplacementDate == date.Date && r.Status != "Cancelled" && r.StartTime < end && start < r.EndTime)
            .ToListAsync();
        if (replacements.Any(r => r.TeacherId == source.TeacherId && !(r.SourceTimetableId == source.TimetableId && r.SourceOccurrenceDate == date.Date)))
            return "Teacher conflict - this teacher has a replacement class at this time";
        if (replacements.Any(r => r.SourceTimetable != null && r.SourceTimetable.ClassId == source.ClassId && !(r.SourceTimetableId == source.TimetableId && r.SourceOccurrenceDate == date.Date)))
            return "Class/time conflict - this class has a replacement class at this time";
        return null;
    }

    private async Task<string?> RoomNameAsync(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return null;
        return await _db.Classrooms.AsNoTracking().Where(r => r.ClassroomId == roomId).Select(r => r.RoomName).FirstOrDefaultAsync();
    }

    private async Task EnsurePendingAuditAsync(Timetable t, DateTime date, string reason)
    {
        if (await _db.TimetableAudits.AnyAsync(a => a.TimetableId == t.TimetableId && a.OccurrenceDate == date.Date && a.Status == "Pending" && a.Reason == reason)) return;
        _db.TimetableAudits.Add(new TimetableAudit
        {
            AuditId = await FormattedIdGenerator.NextAsync(_db.TimetableAudits, x => x.AuditId, "AUD"),
            TermId = t.TermId,
            TimetableId = t.TimetableId,
            OccurrenceDate = date.Date,
            IssueType = reason.Contains("Holiday", StringComparison.OrdinalIgnoreCase) ? "Holiday Review" : reason.Contains("classroom", StringComparison.OrdinalIgnoreCase) ? "Classroom" : "Conflict",
            Status = "Pending",
            BeforeValue = $"{date:dd MMM yyyy} | {t.StartTime:hh\\:mm}-{t.EndTime:hh\\:mm}",
            Reason = reason
        });
        await _db.SaveChangesAsync();
    }

    private async Task ResolveAuditAsync(Timetable t, DateTime date, string before, string after, string reason)
    {
        var pending = await _db.TimetableAudits.Where(a => a.TimetableId == t.TimetableId && a.OccurrenceDate == date.Date && a.Status == "Pending").ToListAsync();
        if (pending.Count == 0)
        {
            pending.Add(new TimetableAudit
            {
                AuditId = await FormattedIdGenerator.NextAsync(_db.TimetableAudits, x => x.AuditId, "AUD"),
                TermId = t.TermId,
                TimetableId = t.TimetableId,
                OccurrenceDate = date.Date,
                IssueType = "Manual Change",
                CreatedAt = DateTime.Now
            });
            _db.TimetableAudits.Add(pending[0]);
        }
        foreach (var audit in pending)
        {
            audit.Status = "Resolved";
            audit.BeforeValue ??= before;
            audit.AfterValue = after;
            audit.Reason = reason;
            audit.ChangedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            audit.ResolvedAt = DateTime.Now;
        }
    }

    private async Task EnsureReplacementRequiredAuditAsync(Timetable t, DateTime date, string before)
    {
        if (await _db.TimetableAudits.AnyAsync(a => a.TimetableId == t.TimetableId && a.OccurrenceDate == date.Date && a.IssueType == "Replacement Required" && a.Status == "Pending")) return;
        _db.TimetableAudits.Add(new TimetableAudit
        {
            AuditId = await FormattedIdGenerator.NextAsync(_db.TimetableAudits, x => x.AuditId, "AUD"),
            TermId = t.TermId,
            TimetableId = t.TimetableId,
            OccurrenceDate = date.Date,
            IssueType = "Replacement Required",
            Status = "Pending",
            BeforeValue = before,
            Reason = "Cancelled lesson is waiting for a replacement class.",
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }

    private async Task ResolveReplacementAuditAsync(Timetable t, DateTime sourceDate, string before, string after)
    {
        var audit = await _db.TimetableAudits.FirstOrDefaultAsync(a => a.TimetableId == t.TimetableId && a.OccurrenceDate == sourceDate.Date && a.IssueType == "Replacement Required" && a.Status == "Pending");
        if (audit is null)
        {
            audit = new TimetableAudit
            {
                AuditId = await FormattedIdGenerator.NextAsync(_db.TimetableAudits, x => x.AuditId, "AUD"),
                TermId = t.TermId,
                TimetableId = t.TimetableId,
                OccurrenceDate = sourceDate.Date,
                IssueType = "Replacement Scheduled",
                CreatedAt = DateTime.Now
            };
            _db.TimetableAudits.Add(audit);
        }
        audit.Status = "Resolved";
        audit.BeforeValue ??= before;
        audit.AfterValue = after;
        audit.Reason = "Replacement class scheduled";
        audit.ChangedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        audit.ResolvedAt = DateTime.Now;
    }

    private async Task NotifyReplacementScheduledAsync(Timetable t, DateTime sourceDate, DateTime replacementDate, TimeSpan start, TimeSpan end, string? room)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { t.TeacherId };
        var studentIds = await _db.StudentClasses.AsNoTracking().Where(sc => sc.ClassId == t.ClassId).Select(sc => sc.UserId).ToListAsync();
        foreach (var id in studentIds) if (!string.IsNullOrWhiteSpace(id)) recipients.Add(id);
        var subject = await _db.Courses.AsNoTracking().Where(c => c.CourseId == t.CourseId).Select(c => c.CourseName).FirstOrDefaultAsync() ?? t.CourseId;
        var message = $"Replacement class scheduled for {subject}: {replacementDate:dd MMM yyyy}, {start:hh\\:mm}-{end:hh\\:mm}{(string.IsNullOrWhiteSpace(room) ? "" : $", {room}")}. This replaces the cancelled lesson on {sourceDate:dd MMM yyyy}.";
        foreach (var userId in recipients)
        {
            if (await _db.TimetableNotifications.AnyAsync(n => n.UserId == userId && n.TimetableId == t.TimetableId && n.OccurrenceDate == replacementDate.Date && n.Message == message)) continue;
            _db.TimetableNotifications.Add(new TimetableNotification
            {
                NotificationId = await FormattedIdGenerator.NextAsync(_db.TimetableNotifications, x => x.NotificationId, "NTF"),
                UserId = userId,
                Type = "ReplacementScheduled",
                Message = message,
                TermId = t.TermId,
                TimetableId = t.TimetableId,
                OccurrenceDate = replacementDate.Date,
                CreatedAt = DateTime.Now
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task NotifyAffectedUsersAsync(Timetable t, DateTime date, string before, string after, string status)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { t.TeacherId };
        var studentIds = await _db.StudentClasses.AsNoTracking().Where(sc => sc.ClassId == t.ClassId).Select(sc => sc.UserId).ToListAsync();
        foreach (var id in studentIds) if (!string.IsNullOrWhiteSpace(id)) recipients.Add(id);

        var subject = await _db.Courses.AsNoTracking().Where(c => c.CourseId == t.CourseId).Select(c => c.CourseName).FirstOrDefaultAsync() ?? t.CourseId;
        var message = status == "Cancelled"
            ? $"{subject} on {date:dd MMM yyyy} has been cancelled."
            : $"{subject} on {date:dd MMM yyyy} was updated. {before} -> {after}";

        foreach (var userId in recipients)
        {
            if (await _db.TimetableNotifications.AnyAsync(n => n.UserId == userId && n.TimetableId == t.TimetableId && n.OccurrenceDate == date.Date && n.Message == message)) continue;
            _db.TimetableNotifications.Add(new TimetableNotification
            {
                NotificationId = await FormattedIdGenerator.NextAsync(_db.TimetableNotifications, x => x.NotificationId, "NTF"),
                UserId = userId,
                Type = status == "Cancelled" ? "LessonCancelled" : "LessonUpdated",
                Message = message,
                TermId = t.TermId,
                TimetableId = t.TimetableId,
                OccurrenceDate = date.Date,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
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


    private async Task SyncSemesterStatusesAsync(DateTime today)
    {
        var terms = await _db.AcademicTerms.OrderBy(t => t.StartDate).ToListAsync();
        var changed = false;

        // Backward compatibility with the previous lifecycle names.
        foreach (var term in terms)
        {
            if (term.Status == "Upcoming") { term.Status = "Incoming"; changed = true; }
            else if (term.Status == "Completed") { term.Status = "Warning"; changed = true; }
            else if (term.Status == "WarningRestored") { term.Status = "RestoredIncoming"; changed = true; }
        }

        // Expired Active/Incoming semesters automatically move to History (Warning).
        foreach (var term in terms.Where(t => (t.Status is "Active" or "Incoming") && t.EndDate.Date < today.Date))
        {
            term.Status = "Warning";
            changed = true;
        }

        // Repair legacy duplicate Active states: keep only the earliest valid Active.
        var activeTerms = terms.Where(t => t.Status == "Active" && t.EndDate.Date >= today.Date)
            .OrderBy(t => t.StartDate).ToList();
        if (activeTerms.Count > 1)
        {
            foreach (var extra in activeTerms.Skip(1))
            {
                extra.Status = extra.StartDate.Date > today.Date ? "Incoming" : "Warning";
                changed = true;
            }
            activeTerms = activeTerms.Take(1).ToList();
        }

        // If there is no Active semester, the earliest Incoming semester whose date has arrived becomes Active.
        if (activeTerms.Count == 0)
        {
            var candidate = terms
                .Where(t => t.Status == "Incoming" && t.StartDate.Date <= today.Date && t.EndDate.Date >= today.Date)
                .OrderBy(t => t.StartDate)
                .FirstOrDefault();
            if (candidate is not null)
            {
                candidate.Status = "Active";
                changed = true;
            }
        }

        if (changed) await _db.SaveChangesAsync();
    }

    private async Task<string?> ValidateSemesterDatesAsync(
        string? termId, string name, DateTime startDate, DateTime endDate, DateTime today, bool checkName = true)
    {
        if (name.Length < 3 || name.Length > 100) return "Semester name must be between 3 and 100 characters.";
        if (startDate < today.Date) return "Start date cannot be in the past.";
        if (endDate <= startDate) return "End date must be later than the start date.";
        if (endDate > startDate.AddYears(1)) return "Semester duration cannot exceed one year.";

        if (checkName && await _db.AcademicTerms.AnyAsync(t => t.TermId != termId && t.Name.ToLower() == name.ToLower()))
            return "A semester with this name already exists.";

        var conflict = await _db.AcademicTerms.AsNoTracking()
            .Where(t => t.TermId != termId && t.Status != "Abandoned" &&
                        t.StartDate <= endDate && startDate <= t.EndDate)
            .OrderBy(t => t.StartDate)
            .FirstOrDefaultAsync();

        if (conflict is not null)
        {
            var overlapStart = startDate > conflict.StartDate.Date ? startDate : conflict.StartDate.Date;
            var overlapEnd = endDate < conflict.EndDate.Date ? endDate : conflict.EndDate.Date;
            var overlapText = overlapStart == overlapEnd
                ? $"{overlapStart:dd MMM yyyy} is already used by {conflict.Name}."
                : $"The overlapping period is {overlapStart:dd MMM yyyy} - {overlapEnd:dd MMM yyyy}.";

            var resolution = conflict.Status == "Warning"
                ? $"Please change the new semester dates, change {conflict.Name}'s dates if appropriate, or mark {conflict.Name} as Abandoned in Completed / History if that old semester is no longer valid."
                : "Please choose dates that begin after the conflicting semester has fully ended.";

            return $"The selected dates conflict with {conflict.Name} ({conflict.StartDate:dd MMM yyyy} - {conflict.EndDate:dd MMM yyyy}). {overlapText} {resolution}";
        }

        return null;
    }

    private async Task RebuildWeeksAsync(AcademicTerm term)
    {
        var oldWeeks = await _db.AcademicWeeks.Where(w => w.TermId == term.TermId).ToListAsync();
        if (oldWeeks.Count > 0) _db.AcademicWeeks.RemoveRange(oldWeeks);

        var weekNo = 1;
        for (var start = term.StartDate.Date; start <= term.EndDate.Date; start = start.AddDays(7), weekNo++)
        {
            _db.AcademicWeeks.Add(new AcademicWeek
            {
                AcademicWeekId = await FormattedIdGenerator.NextAsync(_db.AcademicWeeks, x => x.AcademicWeekId, "AWK"),
                TermId = term.TermId,
                WeekNumber = weekNo,
                StartDate = start,
                EndDate = start.AddDays(6) > term.EndDate.Date ? term.EndDate.Date : start.AddDays(6),
                WeekType = "Teaching"
            });
        }
    }

}
