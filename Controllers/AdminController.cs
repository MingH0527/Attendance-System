using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

public class AdminController : Controller
{
    private readonly DB _context;
    private readonly ITimetableValidationService _validationService;

    public AdminController(
        DB context,
        ITimetableValidationService validationService)
    {
        _context = context;
        _validationService = validationService;
    }

    public IActionResult Index()
    {
        return View("Admin");
    }

    public IActionResult Timetable()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers = await _context.Users
            .Where(u => u.Position == "Teacher")
            .OrderBy(u => u.Username)
            .Select(u => new TeacherLookupDto
            {
                TeacherId = u.UserId,
                TeacherName = u.Username
            })
            .ToListAsync();

        return Json(teachers);
    }

    [HttpGet]
    public async Task<IActionResult> GetCourses()
    {
        var courses = await _context.Courses
            .OrderBy(c => c.CourseName)
            .Select(c => new SimpleLookupDto
            {
                Id = c.CourseId,
                Name = c.CourseName
            })
            .ToListAsync();

        return Json(courses);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCourse(
        [FromBody] SimpleLookupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Course name is required.");
        }

        string courseName = dto.Name.Trim();

        bool alreadyExists = await _context.Courses
            .AnyAsync(c => c.CourseName == courseName);

        if (alreadyExists)
        {
            return BadRequest("This course already exists.");
        }

        var course = new Course
        {
            CourseName = courseName
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        return Json(new SimpleLookupDto
        {
            Id = course.CourseId,
            Name = course.CourseName
        });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var course = await _context.Courses.FindAsync(id);

        if (course is null)
        {
            return NotFound("Course not found.");
        }

        bool inUse = await _context.Timetables
            .AnyAsync(t => t.CourseId == id);

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this course because it is used in the timetable.");
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Course deleted." });
    }

    [HttpGet]
    public async Task<IActionResult> GetClasses()
    {
        var classes = await _context.ClassGroups
            .OrderBy(c => c.ClassName)
            .Select(c => new SimpleLookupDto
            {
                Id = c.ClassId,
                Name = c.ClassName
            })
            .ToListAsync();

        return Json(classes);
    }

    [HttpPost]
    public async Task<IActionResult> CreateClass(
        [FromBody] SimpleLookupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Class name is required.");
        }

        string className = dto.Name.Trim();

        bool alreadyExists = await _context.ClassGroups
            .AnyAsync(c => c.ClassName == className);

        if (alreadyExists)
        {
            return BadRequest("This class already exists.");
        }

        var classGroup = new ClassGroup
        {
            ClassName = className
        };

        _context.ClassGroups.Add(classGroup);
        await _context.SaveChangesAsync();

        return Json(new SimpleLookupDto
        {
            Id = classGroup.ClassId,
            Name = classGroup.ClassName
        });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClass(int id)
    {
        var classGroup = await _context.ClassGroups.FindAsync(id);

        if (classGroup is null)
        {
            return NotFound("Class not found.");
        }

        bool inUse = await _context.Timetables
            .AnyAsync(t => t.ClassId == id);

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this class because it is used in the timetable.");
        }

        _context.ClassGroups.Remove(classGroup);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Class deleted." });
    }

    [HttpGet]
    public async Task<IActionResult> GetClassrooms()
    {
        var rooms = await _context.Classrooms
            .OrderBy(c => c.RoomName)
            .Select(c => new SimpleLookupDto
            {
                Id = c.ClassroomId,
                Name = c.RoomName
            })
            .ToListAsync();

        return Json(rooms);
    }

    [HttpPost]
    public async Task<IActionResult> CreateClassroom(
        [FromBody] SimpleLookupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Classroom name is required.");
        }

        string roomName = dto.Name.Trim();

        bool alreadyExists = await _context.Classrooms
            .AnyAsync(c => c.RoomName == roomName);

        if (alreadyExists)
        {
            return BadRequest("This classroom already exists.");
        }

        var room = new Classroom
        {
            RoomName = roomName
        };

        _context.Classrooms.Add(room);
        await _context.SaveChangesAsync();

        return Json(new SimpleLookupDto
        {
            Id = room.ClassroomId,
            Name = room.RoomName
        });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClassroom(int id)
    {
        var room = await _context.Classrooms.FindAsync(id);

        if (room is null)
        {
            return NotFound("Classroom not found.");
        }

        bool inUse = await _context.Timetables
            .AnyAsync(t => t.ClassroomId == id);

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this classroom because it is used in the timetable.");
        }

        _context.Classrooms.Remove(room);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Classroom deleted." });
    }

    [HttpGet]
    public async Task<IActionResult> GetTimetableGrid(string classType)
    {
        if (!Enum.TryParse(
                classType,
                true,
                out ClassType parsedType))
        {
            return BadRequest(
                "Invalid class type. Use School or Tuition.");
        }

        var entries = await _context.Timetables
            .AsNoTracking()
            .Where(t => t.ClassType == parsedType)
            .Include(t => t.Course)
            .Include(t => t.ClassGroup)
            .Include(t => t.Classroom)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .ToListAsync();

        var teacherIds = entries
            .Select(t => t.TeacherId)
            .Distinct()
            .ToList();

        var teacherNames = await _context.Users
            .Where(u => teacherIds.Contains(u.UserId))
            .ToDictionaryAsync(
                u => u.UserId,
                u => u.Username);

        var result = entries.Select(t => new TimetableSlotDto
        {
            TimetableId = t.TimetableId,
            TeacherId = t.TeacherId,
            TeacherName = teacherNames.GetValueOrDefault(
                t.TeacherId,
                "Unknown"),
            CourseId = t.CourseId,
            CourseName = t.Course?.CourseName ?? string.Empty,
            ClassId = t.ClassId,
            ClassName = t.ClassGroup?.ClassName ?? string.Empty,
            ClassroomId = t.ClassroomId,
            RoomName = t.Classroom?.RoomName,
            DayOfWeek = t.DayOfWeek,
            StartTime = t.StartTime.ToString(@"hh\:mm"),
            EndTime = t.EndTime.ToString(@"hh\:mm"),
            ClassType = t.ClassType
        });

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTimetableEntry(
        [FromBody] TimetableRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(GetModelErrors());
        }

        var validation =
            await _validationService.ValidateAsync(request);

        if (!validation.IsValid)
        {
            return BadRequest(validation.ErrorMessage);
        }

        var entry = new Timetable
        {
            TeacherId = request.TeacherId,
            CourseId = request.CourseId,
            ClassId = request.ClassId,
            ClassroomId = request.ClassroomId,
            DayOfWeek = request.DayOfWeek,
            StartTime = TimeSpan.Parse(request.StartTime),
            EndTime = TimeSpan.Parse(request.EndTime),
            ClassType = request.ClassType
        };

        _context.Timetables.Add(entry);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Timetable entry created.",
            timetableId = entry.TimetableId
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateTimetableEntry(
        [FromBody] TimetableRequest request)
    {
        if (!request.TimetableId.HasValue)
        {
            return BadRequest(
                "TimetableId is required for updates.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(GetModelErrors());
        }

        var entry = await _context.Timetables
            .FindAsync(request.TimetableId.Value);

        if (entry is null)
        {
            return NotFound("Timetable entry not found.");
        }

        var validation =
            await _validationService.ValidateAsync(request);

        if (!validation.IsValid)
        {
            return BadRequest(validation.ErrorMessage);
        }

        entry.TeacherId = request.TeacherId;
        entry.CourseId = request.CourseId;
        entry.ClassId = request.ClassId;
        entry.ClassroomId = request.ClassroomId;
        entry.DayOfWeek = request.DayOfWeek;
        entry.StartTime = TimeSpan.Parse(request.StartTime);
        entry.EndTime = TimeSpan.Parse(request.EndTime);
        entry.ClassType = request.ClassType;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Timetable entry updated."
        });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTimetableEntry(int id)
    {
        var entry = await _context.Timetables.FindAsync(id);

        if (entry is null)
        {
            return NotFound("Timetable entry not found.");
        }

        _context.Timetables.Remove(entry);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Timetable entry deleted."
        });
    }

    private Dictionary<string, string> GetModelErrors()
    {
        return ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors
                    .First()
                    .ErrorMessage);
    }

    // POST: /Admin/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction("Login", "Account");
    }
}