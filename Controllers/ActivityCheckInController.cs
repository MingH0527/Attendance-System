using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

public class ActivityCheckInController : Controller
{
    private readonly DB db;
    private readonly IPasswordHasher<User> passwordHasher;

    public ActivityCheckInController(DB db, IPasswordHasher<User> passwordHasher)
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string token)
    {
        FacilityBooking? booking = await GetBooking(token);
        if (booking == null) return View("InvalidQr");

        if (booking.Status == "Checked In" && booking.Attendance != null)
        {
            return View("AlreadyCheckedIn", booking);
        }

        if (booking.Status != "Approved")
        {
            ViewBag.Message = "This booking is not available for Activity Check-In.";
            return View("InvalidQr");
        }

        return View(new ActivityCheckInLoginViewModel { Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyStudent(ActivityCheckInLoginViewModel model)
    {
        FacilityBooking? booking = await GetBooking(model.Token);
        if (booking == null)
        {
            ViewBag.Message = "Invalid Activity Check-In QR code.";
            return View("InvalidQr");
        }

        if (booking.Status == "Checked In" && booking.Attendance != null)
        {
            return View("AlreadyCheckedIn", booking);
        }

        if (booking.Status != "Approved")
        {
            ViewBag.Message = "This booking is not available for Activity Check-In.";
            return View("InvalidQr");
        }

        string studentId = (model.StudentId ?? "").Trim();
        model.Password ??= "";

        StudentClass? studentClass = await db.StudentClasses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId);

        if (studentClass == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid Student ID or login password.");
            return View("Index", model);
        }

        User? user = await db.Users.FirstOrDefaultAsync(u =>
            u.UserId == studentClass.UserId && !u.IsDeleted && u.Position == "Student");

        if (user == null || user.Status != "Active" ||
            passwordHasher.VerifyHashedPassword(user, user.Password, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid Student ID or login password.");
            return View("Index", model);
        }

        if (!string.Equals(booking.UserId, user.UserId, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "This booking does not belong to the entered student account.");
            return View("Index", model);
        }

        bool hasFace = await db.UserFaceProfiles
            .AnyAsync(p => p.UserId == user.UserId && p.IsActive);

        if (!hasFace)
        {
            ViewBag.Token = model.Token;
            ViewBag.StudentId = studentId;
            return View("FaceNotRegistered");
        }

        await SignInStudent(user);
        HttpContext.Session.SetString("ActivityCheckIn.Token", model.Token);
        HttpContext.Session.SetString("ActivityCheckIn.UserId", user.UserId);

        string returnUrl = Url.Action(nameof(Complete), "ActivityCheckIn", new { token = model.Token })!;
        return RedirectToAction("Verify", "FaceId", new { returnUrl });
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Complete(string token)
    {
        string? pendingToken = HttpContext.Session.GetString("ActivityCheckIn.Token");
        string? pendingUserId = HttpContext.Session.GetString("ActivityCheckIn.UserId");
        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        if (string.IsNullOrWhiteSpace(token) || token != pendingToken || currentUserId != pendingUserId)
        {
            ViewBag.Message = "The Activity Check-In verification session is invalid or has expired.";
            return View("InvalidQr");
        }

        FacilityBooking? booking = await db.FacilityBookings
            .Include(b => b.Facility)
            .Include(b => b.User)
            .Include(b => b.Attendance)
            .FirstOrDefaultAsync(b => b.QrToken == token);

        if (booking == null || booking.UserId != currentUserId)
        {
            ViewBag.Message = "This booking could not be verified for the current student.";
            return View("InvalidQr");
        }

        if (booking.Status == "Checked In" && booking.Attendance != null)
        {
            ClearPending();
            return View("AlreadyCheckedIn", booking);
        }

        if (booking.Status != "Approved")
        {
            ViewBag.Message = "This booking is no longer available for Activity Check-In.";
            return View("InvalidQr");
        }

        if (!FaceIdController.HasValidPaymentFaceVerification(HttpContext))
        {
            string returnUrl = Url.Action(nameof(Complete), "ActivityCheckIn", new { token })!;
            return RedirectToAction("Verify", "FaceId", new { returnUrl });
        }

        FacilityAttendance attendance = new()
        {
            FacilityAttendanceId = await FormattedIdGenerator.NextAsync(db.FacilityAttendances, a => a.FacilityAttendanceId, "FAA"),
            BookingId = booking.BookingId,
            UserId = booking.UserId,
            FacilityId = booking.FacilityId,
            CheckInTime = MalaysiaNow(),
            QrVerified = true,
            FaceVerified = true,
            VerificationMethod = "Student ID + Password + QR + Face ID",
            Status = "Present"
        };

        db.FacilityAttendances.Add(attendance);
        booking.Status = "Checked In";
        await db.SaveChangesAsync();

        FaceIdController.ConsumePaymentFaceVerification(HttpContext);
        ClearPending();

        booking.Attendance = attendance;
        return View("Success", booking);
    }

    private async Task<FacilityBooking?> GetBooking(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return await db.FacilityBookings
            .AsNoTracking()
            .Include(b => b.Facility)
            .Include(b => b.User)
            .Include(b => b.Attendance)
            .FirstOrDefaultAsync(b => b.QrToken == token);
    }

    private async Task SignInStudent(User user)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Position)
        ];

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }

    private void ClearPending()
    {
        HttpContext.Session.Remove("ActivityCheckIn.Token");
        HttpContext.Session.Remove("ActivityCheckIn.UserId");
    }

    private static DateTime MalaysiaNow()
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"); }
        catch { zone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
    }
}
