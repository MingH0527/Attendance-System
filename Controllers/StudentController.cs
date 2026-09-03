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

    /// <summary>
    /// student home
    /// </summary>
    /// <returns></returns>

    [HttpGet]
    public IActionResult Index()
    {
        return View("Student");
    }


    //apply leave-get

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


    //apply leave-post

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

        // Start date cannot be in the past
        if (model.StartDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.StartDate),
                "Start date cannot be in the past."
            );
        }

        // End date cannot be before start date
        if (model.EndDate.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "End date cannot be earlier than start date."
            );
        }

        if(model.EndDate.Date>model.StartDate.Date.AddDays(6))
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "Leave cannot esceed 7 days"
             );
        }

        string? documentName = null;

       //file validation

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

        //if validation failed

        if (!ModelState.IsValid)
        {
            model.LeaveHistory =
                await db.LeaveApplications
                    .Where(l => l.StudentId == studentId)
                    .OrderByDescending(l => l.AppliedAt)
                    .ToListAsync();

            return View(model);
        }


        //save document

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


        //create leave

        LeaveApplication leave = new()
        {
            StudentId = studentId,

            LeaveType =
                model.LeaveType,

            StartDate =
                model.StartDate,

            EndDate =
                model.EndDate,

            Reason =
                model.Reason,

            SupportingDocument =
                documentName,

            Status =
                "Pending",

            AppliedAt =
                DateTime.Now
        };

        db.LeaveApplications.Add(leave);

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Leave application submitted successfully.";

        return RedirectToAction(nameof(ApplyLeave));
    }


    //cancel leave

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


        // Delete uploaded document
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


    //logout
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