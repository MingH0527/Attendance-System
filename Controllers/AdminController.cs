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

    //leave management

    [HttpGet]
    public async Task<IActionResult> LeaveManagement(int page = 1)
    {
        const int pageSize = 10;

        int totalCount =
            await _context.LeaveApplications.CountAsync();

        int totalPages =
            (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalPages < 1)
        {
            totalPages = 1;
        }
        page = Math.Clamp(page, 1, totalPages);

        // one page only can have 10 data
        var leaves =
            await _context.LeaveApplications
                .AsNoTracking()
                .Include(l => l.Student)
                .OrderByDescending(l => l.AppliedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;

        return View(leaves);
    }


    //approve leave

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLeave(int id)
    {
        LeaveApplication? leave =
            await _context.LeaveApplications
                .FirstOrDefaultAsync(l => l.LeaveId == id);

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

            return RedirectToAction(nameof(LeaveManagement));
        }

        leave.Status = "Approved";

        await _context.SaveChangesAsync();

        TempData["LeaveSuccess"] =
            "Leave application approved successfully.";

        return RedirectToAction(nameof(LeaveManagement));
    }


    //reject leave

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectLeave(int id)
    {
        LeaveApplication? leave =
            await _context.LeaveApplications
                .FirstOrDefaultAsync(l => l.LeaveId == id);

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

            return RedirectToAction(nameof(LeaveManagement));
        }

        leave.Status = "Rejected";

        await _context.SaveChangesAsync();

        TempData["LeaveSuccess"] =
            "Leave application rejected successfully.";

        return RedirectToAction(nameof(LeaveManagement));
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
    [HttpGet]
    public async Task<IActionResult> GetTeacherCourses(string teacherId)
    {
        if (string.IsNullOrWhiteSpace(teacherId))
        {
            return BadRequest("Teacher ID is required.");
        }

        var courses = await _context.TeacherCourses
            .AsNoTracking()
            .Where(tc => tc.UserId == teacherId)
            .Include(tc => tc.Course)
            .OrderBy(tc => tc.Course!.CourseName)
            .Select(tc => new SimpleLookupDto
            {
                Id = tc.CourseId,
                Name = tc.Course!.CourseName
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
            return BadRequest(
                "Course name is required."
            );
        }

        string courseName =
            dto.Name.Trim();

        bool alreadyExists =
            await _context.Courses.AnyAsync(
                c => c.CourseName == courseName
            );

        if (alreadyExists)
        {
            return BadRequest(
                "This course already exists."
            );
        }

        Course course = new()
        {
            CourseId = await FormattedIdGenerator.NextAsync(
                _context.Courses,
                c => c.CourseId,
                "CRS"
            ),
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
    public async Task<IActionResult> DeleteCourse(
        string id)
    {
        Course? course =
            await _context.Courses.FindAsync(id);

        if (course == null)
        {
            return NotFound(
                "Course not found."
            );
        }

        bool inUse =
            await _context.Timetables.AnyAsync(
                t => t.CourseId == id
            );

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this course because " +
                "it is used in the timetable."
            );
        }

        _context.Courses.Remove(course);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Course deleted."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetClasses()
    {
        var classes =
            await _context.ClassGroups
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
            return BadRequest(
                "Class name is required."
            );
        }

        string className =
            dto.Name.Trim();

        bool alreadyExists =
            await _context.ClassGroups.AnyAsync(
                c => c.ClassName == className
            );

        if (alreadyExists)
        {
            return BadRequest(
                "This class already exists."
            );
        }

        ClassGroup classGroup = new()
        {
            ClassId = await FormattedIdGenerator.NextAsync(
                _context.ClassGroups,
                c => c.ClassId,
                "CLS"
            ),
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
    public async Task<IActionResult> DeleteClass(
        string id)
    {
        ClassGroup? classGroup =
            await _context.ClassGroups.FindAsync(id);

        if (classGroup == null)
        {
            return NotFound(
                "Class not found."
            );
        }

        bool inUse =
            await _context.Timetables.AnyAsync(
                t => t.ClassId == id
            );

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this class because " +
                "it is used in the timetable."
            );
        }

        _context.ClassGroups.Remove(classGroup);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Class deleted."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetClassrooms()
    {
        var rooms =
            await _context.Classrooms
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
            return BadRequest(
                "Classroom name is required."
            );
        }

        string roomName =
            dto.Name.Trim();

        bool alreadyExists =
            await _context.Classrooms.AnyAsync(
                c => c.RoomName == roomName
            );

        if (alreadyExists)
        {
            return BadRequest(
                "This classroom already exists."
            );
        }

        Classroom room = new()
        {
            ClassroomId = await FormattedIdGenerator.NextAsync(
                _context.Classrooms,
                c => c.ClassroomId,
                "ROM"
            ),
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
    public async Task<IActionResult> DeleteClassroom(
        string id)
    {
        Classroom? room =
            await _context.Classrooms.FindAsync(id);

        if (room == null)
        {
            return NotFound(
                "Classroom not found."
            );
        }

        bool inUse =
            await _context.Timetables.AnyAsync(
                t => t.ClassroomId == id
            );

        if (inUse)
        {
            return BadRequest(
                "Cannot delete this classroom because " +
                "it is used in the timetable."
            );
        }

        _context.Classrooms.Remove(room);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Classroom deleted."
        });
    }

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
            await _context.Timetables
                .AsNoTracking()
                .Where(
                    t => t.ClassType == parsedType
                )
                .Include(t => t.Course)
                .Include(t => t.ClassGroup)
                .Include(t => t.Classroom)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartTime)
                .ToListAsync();

        List<string> teacherIds =
            entries
                .Select(t => t.TeacherId)
                .Distinct()
                .ToList();

        Dictionary<string, string> teacherNames =
            await _context.Users
                .Where(
                    u => teacherIds.Contains(
                        u.UserId
                    )
                )
                .ToDictionaryAsync(
                    u => u.UserId,
                    u => u.Username
                );

        var result =
            entries.Select(
                t => new TimetableSlotDto
                {
                    TimetableId =
                        t.TimetableId,

                    TeacherId =
                        t.TeacherId,

                    TeacherName =
                        teacherNames.GetValueOrDefault(
                            t.TeacherId,
                            "Unknown"
                        ),

                    CourseId =
                        t.CourseId,

                    CourseName =
                        t.Course?.CourseName ??
                        string.Empty,

                    ClassId =
                        t.ClassId,

                    ClassName =
                        t.ClassGroup?.ClassName ??
                        string.Empty,

                    ClassroomId =
                        t.ClassroomId,

                    RoomName =
                        t.Classroom?.RoomName,

                    DayOfWeek =
                        t.DayOfWeek,

                    StartTime =
                        t.StartTime.ToString(
                            @"hh\:mm"
                        ),

                    EndTime =
                        t.EndTime.ToString(
                            @"hh\:mm"
                        ),

                    ClassType =
                        t.ClassType
                }
            );

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult>
        CreateTimetableEntry(
            [FromBody] TimetableRequest request)
        {

        if (!ModelState.IsValid)
        {
            return BadRequest(
                GetModelErrors()
            );
        }


        // Check whether this teacher registered this course
        bool teacherCanTeachCourse =
            await _context.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == request.TeacherId &&
                    tc.CourseId == request.CourseId
                );


        if (!teacherCanTeachCourse)
        {
            return BadRequest(
                "This teacher is not registered to teach this course."
            );
        }


        var validation =
            await _validationService
                .ValidateAsync(request);

        if (!validation.IsValid)
        {
            return BadRequest(
                validation.ErrorMessage
            );
        }

        Timetable entry = new()
        {
            TimetableId = await FormattedIdGenerator.NextAsync(
                _context.Timetables,
                t => t.TimetableId,
                "TTB"
            ),
            TeacherId = request.TeacherId,
            CourseId = request.CourseId,
            ClassId = request.ClassId,
            ClassroomId = request.ClassroomId,
            DayOfWeek = request.DayOfWeek,

            StartTime =
                TimeSpan.Parse(
                    request.StartTime
                ),

            EndTime =
                TimeSpan.Parse(
                    request.EndTime
                ),

            ClassType = request.ClassType
        };

        _context.Timetables.Add(entry);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry created.",

            timetableId =
                entry.TimetableId
        });
    }

    [HttpPost]
    public async Task<IActionResult>
        UpdateTimetableEntry(
            [FromBody] TimetableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TimetableId))
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
            await _context.TeacherCourses
                .AnyAsync(tc =>
                    tc.UserId == request.TeacherId &&
                    tc.CourseId == request.CourseId
                );

        if (!teacherCanTeachCourse)
        {
            return BadRequest(
                "This teacher is not registered to teach this course."
            );
        }

        Timetable? entry =
            await _context.Timetables.FindAsync(
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
                .ValidateAsync(request);

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

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry updated."
        });
    }

    [HttpPost]
    public async Task<IActionResult>
        DeleteTimetableEntry(
            string id)
    {
        Timetable? entry =
            await _context.Timetables.FindAsync(id);

        if (entry == null)
        {
            return NotFound(
                "Timetable entry not found."
            );
        }

        _context.Timetables.Remove(entry);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry deleted."
        });
    }

    private Dictionary<string, string>
        GetModelErrors()
    {
        return ModelState
            .Where(
                item =>
                    item.Value?.Errors.Count > 0
            )
            .ToDictionary(
                item => item.Key,

                item =>
                    item.Value!
                        .Errors
                        .First()
                        .ErrorMessage
            );
    }
    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            System.Security.Claims.ClaimTypes.NameIdentifier
        );
    }



    [HttpGet]
    public async Task<IActionResult> GetNonTeacherUsers()
    {
        var users = await _context.Users
            .Where(u => u.Position == "Student" && !u.IsDeleted)
            .OrderBy(u => u.Username)
            .Select(u => new TeacherLookupDto
            {
                TeacherId = u.UserId,
                TeacherName = u.Username
            })
            .ToListAsync();

        return Json(users);
    }

    [HttpGet]
    public IActionResult Createteacher()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> PromoteToTeacher(string id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null || user.IsDeleted)
        {
            return NotFound("User not found.");
        }

        if (user.Position == "Teacher")
        {
            return BadRequest("This user is already a teacher.");
        }
        if (user.Position == "Admin")
        {
            return BadRequest("Admin cannot be promoted to Teacher.");
        }

        user.Position = "Teacher";
        await _context.SaveChangesAsync();

        return Ok();
    }
    [HttpGet]
    public async Task<IActionResult> GetAllUsersWithRoles()
    {
        var users = await _context.Users
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Username)
            .Select(u => new UserRoleDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Position = u.Position
            })
            .ToListAsync();

        return Json(users);
    }

    [HttpPost]
    public async Task<IActionResult> CancelTeacherRole(string id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null || user.IsDeleted)
        {
            return NotFound("User not found.");
        }

        if (user.Position == "Admin")
        {
            return BadRequest("Cannot change an Admin's role.");
        }

        if (user.Position != "Teacher")
        {
            return BadRequest("This user is not currently a teacher.");
        }

        user.Position = "Student";
        await _context.SaveChangesAsync();

        return Ok();
    }
}