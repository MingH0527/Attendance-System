using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

[Authorize]
public class StudentController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment environment;

    public StudentController(
        DB db,
        IWebHostEnvironment environment)
    {
        this.db = db;
        this.environment = environment;
    }


    // ==============================
    // STUDENT HOME
    // ==============================

    [HttpGet]
    public IActionResult Index()
    {
        return View("Student");
    }

    // ==============================
    // STUDENT TIMETABLE
    // ==============================

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Timetable()
    {
        string? studentId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }


        // Find which class this student belongs to
        StudentClass? studentClass =
            await db.StudentClasses
                .AsNoTracking()
                .Include(sc => sc.ClassGroup)
                .Include(sc => sc.Program)
                .FirstOrDefaultAsync(
                    sc => sc.UserId == studentId
                );


        // Student has not been assigned to a class
        if (studentClass == null)
        {
            ViewBag.ClassName =
                "Not Assigned";

            ViewBag.ProgramName =
                "Not Assigned";

            ViewBag.TermName =
                "No Active Semester";

            return View(
                new List<Timetable>()
            );
        }


        // Get timetable belonging to this student's class
        // Only show the Active semester
        List<Timetable> timetable =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.ClassId == studentClass.ClassId &&
                    t.Term != null &&
                    t.Term.Status == "Active"
                )
                .Include(t => t.Term)
                .Include(t => t.Course)
                .Include(t => t.Classroom)
                .Include(t => t.Teacher)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartTime)
                .ToListAsync();


        ViewBag.ClassName =
            studentClass.ClassGroup?.ClassName
            ?? studentClass.ClassId;

        ViewBag.ProgramName =
            studentClass.Program?.ProgramName
            ?? studentClass.ProgramId;

        ViewBag.TermName =
            timetable
                .FirstOrDefault()?
                .Term?
                .Name
            ?? "No Active Semester";

        var activeTermIds = timetable.Where(t => t.TermId != null).Select(t => t.TermId!).Distinct().ToList();
        ViewBag.TimetableNotifications = await db.TimetableNotifications
            .AsNoTracking()
            .Where(n => n.UserId == studentId && n.TermId != null && activeTermIds.Contains(n.TermId))
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .ToListAsync();

        return View(timetable);
    }


    // ==============================
    // APPLY LEAVE - GET
    // ==============================

    [HttpGet]
    public async Task<IActionResult> ApplyLeave()
    {
        string? studentId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized();
        }

        ApplyLeaveViewModel model = new()
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today,

            LeaveHistory = await db.LeaveApplications
                .Where(l => l.StudentId == studentId)
                .OrderByDescending(l => l.AppliedAt)
                .ToListAsync()
        };

        return View(model);
    }


    // ==============================
    // APPLY LEAVE - POST
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyLeave(
        ApplyLeaveViewModel model)
    {
        string? studentId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized();
        }


        // ==============================
        // DATE VALIDATION
        // ==============================

        // Cannot select a past Start Date
        if (model.StartDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.StartDate),
                "Start date cannot be in the past."
            );
        }


        // End Date cannot be earlier than Start Date
        if (model.EndDate.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "End date cannot be earlier than start date."
            );
        }


        // Maximum 7 days
        // Start Date itself counts as Day 1
        if (model.EndDate.Date >
            model.StartDate.Date.AddDays(6))
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "Leave application cannot exceed 7 days."
            );
        }


        // ==============================
        // FILE VALIDATION
        // ==============================

        string? documentName = null;

        if (model.SupportingDocument != null &&
            model.SupportingDocument.Length > 0)
        {
            string extension =
                Path.GetExtension(
                    model.SupportingDocument.FileName)
                .ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".pdf",
                ".jpg",
                ".jpeg",
                ".png"
            };


            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    nameof(model.SupportingDocument),
                    "Only PDF, JPG, JPEG and PNG files are allowed."
                );
            }


            // Maximum 5MB
            if (model.SupportingDocument.Length >
                5 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    nameof(model.SupportingDocument),
                    "File size cannot exceed 5MB."
                );
            }
        }


        // ==============================
        // VALIDATION FAILED
        // ==============================

        if (!ModelState.IsValid)
        {
            model.LeaveHistory =
                await db.LeaveApplications
                    .Where(l => l.StudentId == studentId)
                    .OrderByDescending(l => l.AppliedAt)
                    .ToListAsync();

            return View(model);
        }


        // ==============================
        // SAVE DOCUMENT
        // ==============================

        if (model.SupportingDocument != null &&
            model.SupportingDocument.Length > 0)
        {
            string extension =
                Path.GetExtension(
                    model.SupportingDocument.FileName)
                .ToLowerInvariant();

            string folder =
                Path.Combine(
                    environment.WebRootPath,
                    "uploads",
                    "leave"
                );

            Directory.CreateDirectory(folder);


            documentName =
                Guid.NewGuid().ToString() + extension;


            string filePath =
                Path.Combine(
                    folder,
                    documentName
                );


            using FileStream stream =
                new(
                    filePath,
                    FileMode.Create
                );


            await model.SupportingDocument
                .CopyToAsync(stream);
        }


        // ==============================
        // CREATE LEAVE APPLICATION
        // ==============================

        LeaveApplication leave = new()
        {
            StudentId = studentId,

            LeaveType = model.LeaveType,

            StartDate = model.StartDate.Date,

            EndDate = model.EndDate.Date,

            Reason = model.Reason,

            SupportingDocument = documentName,

            Status = "Pending",

            AppliedAt = DateTime.Now
        };


        db.LeaveApplications.Add(leave);

        await db.SaveChangesAsync();


        TempData["SuccessMessage"] =
            "Leave application submitted successfully.";


        return RedirectToAction(nameof(ApplyLeave));
    }


    // ==============================
    // CANCEL LEAVE
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelLeave(int id)
    {
        string? studentId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized();
        }


        LeaveApplication? leave =
            await db.LeaveApplications
                .FirstOrDefaultAsync(l =>
                    l.LeaveId == id &&
                    l.StudentId == studentId
                );


        if (leave == null)
        {
            return NotFound();
        }


        // Student can only cancel Pending leave
        if (!string.Equals(
                leave.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] =
                "Only pending leave applications can be cancelled.";

            return RedirectToAction(nameof(ApplyLeave));
        }


        // ==============================
        // DELETE UPLOADED DOCUMENT
        // ==============================

        if (!string.IsNullOrWhiteSpace(
                leave.SupportingDocument))
        {
            string filePath =
                Path.Combine(
                    environment.WebRootPath,
                    "uploads",
                    "leave",
                    leave.SupportingDocument
                );


            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }


        db.LeaveApplications.Remove(leave);

        await db.SaveChangesAsync();


        TempData["SuccessMessage"] =
            "Leave application cancelled successfully.";


        return RedirectToAction(nameof(ApplyLeave));
    }


    // ==============================
    // LOGOUT
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction(
            "Login",
            "Account"
        );
    }
}