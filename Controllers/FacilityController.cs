using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebAndMobile.Configuration;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize]
public class FacilityController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment environment;

    public FacilityController(DB db, IWebHostEnvironment environment)
    {
        this.db = db;
        this.environment = environment;
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        List<Facility> facilities = await db.Facilities
            .AsNoTracking()
            .Include(f => f.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.UploadedAt))
            .OrderBy(f => f.FacilityId)
            .ToListAsync();

        return View(facilities);
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Details(string id, DateTime? date = null)
    {
        Facility? facility = await db.Facilities
            .AsNoTracking()
            .Include(f => f.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.UploadedAt))
            .FirstOrDefaultAsync(f => f.FacilityId == id);

        if (facility == null) return NotFound();

        DateTime today = MalaysiaNow().Date;
        DateTime selected = (date ?? today).Date;
        if (selected < today || selected > today.AddDays(7)) selected = today;

        List<FacilityBooking> occupiedBookings = await db.FacilityBookings
            .AsNoTracking()
            .Where(b => b.FacilityId == id &&
                        b.BookingDate == selected &&
                        (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In"))
            .ToListAsync();

        HashSet<int> unavailable = occupiedBookings
            .SelectMany(b => Enumerable.Range(b.StartHour / 2, (b.EndHour - b.StartHour) / 2))
            .Select(x => x * 2)
            .ToHashSet();

        string currentUserId = GetCurrentUserId();
        List<FacilityBooking> ownBookings = await db.FacilityBookings
            .AsNoTracking()
            .Where(b => b.UserId == currentUserId &&
                        b.BookingDate == selected &&
                        (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In"))
            .ToListAsync();

        int dailyBookedHours = ownBookings.Sum(b => b.EndHour - b.StartHour);

        HashSet<int> ownBookingStartHours = ownBookings
            .SelectMany(b => Enumerable.Range(b.StartHour / 2, (b.EndHour - b.StartHour) / 2))
            .Select(x => x * 2)
            .ToHashSet();

        HashSet<int> cooldownStartHours = new();
        if (dailyBookedHours < 8)
        {
            foreach (FacilityBooking booking in ownBookings.Where(b => b.EndHour - b.StartHour >= 6))
            {
                // Cooldown is only required after a booking that already reaches the 6-hour single-booking maximum.
                // It blocks the next selectable time slot but never contributes to the 8-hour daily booked total.
                int after = booking.EndHour;
                if (after <= 22) cooldownStartHours.Add(after);
            }
        }

        cooldownStartHours.ExceptWith(ownBookingStartHours);
        cooldownStartHours.ExceptWith(unavailable);

        return View(new FacilityBookingPageViewModel
        {
            Facility = facility,
            SelectedDate = selected,
            UnavailableStartHours = unavailable,
            OwnBookingStartHours = ownBookingStartHours,
            CooldownStartHours = cooldownStartHours,
            DailyBookedHours = dailyBookedHours
        });
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(FacilityBookingRequestViewModel model)
    {
        DateTime today = MalaysiaNow().Date;
        DateTime date = model.BookingDate.Date;

        if (!await db.Facilities.AnyAsync(f => f.FacilityId == model.FacilityId))
        {
            return NotFound();
        }

        if (date < today || date > today.AddDays(7))
        {
            TempData["FacilityError"] = "Bookings can only be made from today up to 7 days in advance.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        if (model.StartHour < 0 || model.StartHour > 22 || model.StartHour % 2 != 0 ||
            model.EndHour < 2 || model.EndHour > 24 || model.EndHour % 2 != 0 ||
            model.StartHour >= model.EndHour)
        {
            TempData["FacilityError"] = "Please select a valid continuous 2-hour time range.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        int requestedHours = model.EndHour - model.StartHour;
        if (requestedHours > 6)
        {
            TempData["FacilityError"] = "A single facility booking can be a maximum of 6 hours (3 continuous slots).";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        string currentUserId = GetCurrentUserId();
        List<FacilityBooking> ownBookings = await db.FacilityBookings
            .AsNoTracking()
            .Where(b => b.UserId == currentUserId &&
                        b.BookingDate == date &&
                        (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In"))
            .ToListAsync();

        int existingDailyHours = ownBookings.Sum(b => b.EndHour - b.StartHour);
        if (existingDailyHours + requestedHours > 8)
        {
            TempData["FacilityError"] = $"Daily booking limit exceeded. You already have {existingDailyHours} booked hour(s) on this date and the daily maximum is 8 hours.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        bool cooldownConflict = ownBookings
            .Where(b => b.EndHour - b.StartHour >= 6)
            .Any(b => model.StartHour >= b.EndHour && model.StartHour < b.EndHour + 2);

        if (cooldownConflict)
        {
            TempData["FacilityError"] = "After a 6-hour booking, the next time slot is reserved as cooldown. Cooldown does not count toward the 8-hour daily limit.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        bool collision = await db.FacilityBookings.AnyAsync(b =>
            b.FacilityId == model.FacilityId &&
            b.BookingDate == date &&
            (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In") &&
            model.StartHour < b.EndHour && model.EndHour > b.StartHour);

        if (collision)
        {
            TempData["FacilityError"] = "One or more selected time slots have just been booked by another student. Please select another available time.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        StudentClass? studentClass = await db.StudentClasses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == currentUserId);

        if (studentClass == null || string.IsNullOrWhiteSpace(studentClass.StudentId))
        {
            TempData["FacilityError"] = "Please complete your student program and class registration before booking a facility.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        FacilityBooking booking = new()
        {
            BookingId = await FormattedIdGenerator.NextAsync(db.FacilityBookings, b => b.BookingId, "FB"),
            UserId = currentUserId,
            StudentId = studentClass.StudentId,
            FacilityId = model.FacilityId,
            BookingDate = date,
            StartHour = model.StartHour,
            EndHour = model.EndHour,
            Status = "Pending",
            CreatedAt = MalaysiaNow()
        };

        db.FacilityBookings.Add(booking);
        await db.SaveChangesAsync();

        TempData["FacilitySuccess"] = $"Booking {booking.BookingId} submitted successfully and is waiting for administrator approval.";
        return RedirectToAction(nameof(MyBookings));
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {
        string userId = GetCurrentUserId();
        List<FacilityBooking> bookings = await db.FacilityBookings
            .AsNoTracking()
            .Include(b => b.Facility)
            .Include(b => b.Attendance)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartHour)
            .ToListAsync();

        return View(bookings);
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string id)
    {
        string userId = GetCurrentUserId();
        FacilityBooking? booking = await db.FacilityBookings
            .FirstOrDefaultAsync(b => b.BookingId == id && b.UserId == userId);

        if (booking == null) return NotFound();

        if (booking.Status is not ("Pending" or "Approved"))
        {
            TempData["FacilityError"] = "Only pending or approved bookings can be cancelled before activity check-in.";
            return RedirectToAction(nameof(MyBookings));
        }

        booking.Status = "Cancelled";
        booking.QrToken = null;
        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = "Booking cancelled successfully.";
        return RedirectToAction(nameof(MyBookings));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> AdminIndex()
    {
        List<Facility> facilities = await db.Facilities
            .AsNoTracking()
            .Include(f => f.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.UploadedAt))
            .OrderBy(f => f.FacilityId)
            .ToListAsync();
        return View(facilities);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Manage(string id)
    {
        Facility? facility = await db.Facilities
            .Include(f => f.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.UploadedAt))
            .FirstOrDefaultAsync(f => f.FacilityId == id);
        return facility == null ? NotFound() : View(facility);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDetails(string facilityId, string location, int capacity, string description)
    {
        Facility? facility = await db.Facilities.FirstOrDefaultAsync(f => f.FacilityId == facilityId);
        if (facility == null) return NotFound();

        facility.Location = (location ?? "").Trim();
        facility.Capacity = Math.Max(1, capacity);
        facility.Description = (description ?? "").Trim();
        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = "Facility details updated successfully.";
        return RedirectToAction(nameof(Manage), new { id = facilityId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImages(string facilityId, List<IFormFile> images)
    {
        Facility? facility = await db.Facilities.Include(f => f.Images).FirstOrDefaultAsync(f => f.FacilityId == facilityId);
        if (facility == null) return NotFound();

        images ??= new List<IFormFile>();
        List<IFormFile> validFiles = images.Where(f => f.Length > 0).ToList();

        if (validFiles.Count == 0)
        {
            TempData["FacilityError"] = "Please select at least one image.";
            return RedirectToAction(nameof(Manage), new { id = facilityId });
        }

        if (facility.Images.Count + validFiles.Count > 4)
        {
            TempData["FacilityError"] = $"A facility can have a maximum of 4 images. This facility currently has {facility.Images.Count} image(s) and you selected {validFiles.Count} more.";
            return RedirectToAction(nameof(Manage), new { id = facilityId });
        }

        string[] allowed = [".jpg", ".png"];
        foreach (IFormFile file in validFiles)
        {
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension) || file.Length > 5 * 1024 * 1024)
            {
                TempData["FacilityError"] = "Only JPG or PNG images up to 5 MB each are allowed.";
                return RedirectToAction(nameof(Manage), new { id = facilityId });
            }
        }

        string folder = Path.Combine(environment.WebRootPath, "uploads", "facility");
        Directory.CreateDirectory(folder);
        bool makeFirstCover = facility.Images.Count == 0;

        foreach (IFormFile file in validFiles)
        {
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string fileName = $"{facilityId}_{Guid.NewGuid():N}{extension}";
            string fullPath = Path.Combine(folder, fileName);
            await using FileStream stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            FacilityImage image = new()
            {
                FacilityImageId = await FormattedIdGenerator.NextAsync(db.FacilityImages, i => i.FacilityImageId, "FIMG"),
                FacilityId = facilityId,
                ImagePath = $"/uploads/facility/{fileName}",
                IsCover = makeFirstCover,
                UploadedAt = MalaysiaNow()
            };
            makeFirstCover = false;
            db.FacilityImages.Add(image);
        }

        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = "Facility image(s) uploaded successfully.";
        return RedirectToAction(nameof(Manage), new { id = facilityId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(string id)
    {
        FacilityImage? selected = await db.FacilityImages.FirstOrDefaultAsync(i => i.FacilityImageId == id);
        if (selected == null) return NotFound();

        List<FacilityImage> images = await db.FacilityImages.Where(i => i.FacilityId == selected.FacilityId).ToListAsync();
        foreach (FacilityImage image in images) image.IsCover = image.FacilityImageId == id;
        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = "Cover image updated successfully.";
        return RedirectToAction(nameof(Manage), new { id = selected.FacilityId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(string id)
    {
        FacilityImage? image = await db.FacilityImages.FirstOrDefaultAsync(i => i.FacilityImageId == id);
        if (image == null) return NotFound();
        string facilityId = image.FacilityId;
        bool wasCover = image.IsCover;

        string relative = image.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(environment.WebRootPath, relative);
        db.FacilityImages.Remove(image);
        await db.SaveChangesAsync();
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

        if (wasCover)
        {
            FacilityImage? next = await db.FacilityImages.Where(i => i.FacilityId == facilityId).OrderBy(i => i.UploadedAt).FirstOrDefaultAsync();
            if (next != null)
            {
                next.IsCover = true;
                await db.SaveChangesAsync();
            }
        }

        TempData["FacilitySuccess"] = "Facility image deleted successfully.";
        return RedirectToAction(nameof(Manage), new { id = facilityId });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> BookingRequests(string? status = null)
    {
        IQueryable<FacilityBooking> query = db.FacilityBookings
            .AsNoTracking()
            .Include(b => b.Facility)
            .Include(b => b.User);

        if (!string.IsNullOrWhiteSpace(status) && status != "All") query = query.Where(b => b.Status == status);

        return View(await query.OrderByDescending(b => b.CreatedAt).ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        FacilityBooking? booking = await db.FacilityBookings.FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();
        if (booking.Status != "Pending")
        {
            TempData["FacilityError"] = "Only pending bookings can be approved.";
            return RedirectToAction(nameof(BookingRequests));
        }

        booking.Status = "Approved";
        booking.ApprovedBy = GetCurrentUserId();
        booking.ApprovedAt = MalaysiaNow();
        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = $"Booking {id} approved successfully.";
        return RedirectToAction(nameof(BookingRequests));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id, string? reason)
    {
        FacilityBooking? booking = await db.FacilityBookings.FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();
        if (booking.Status != "Pending")
        {
            TempData["FacilityError"] = "Only pending bookings can be rejected.";
            return RedirectToAction(nameof(BookingRequests));
        }
        booking.Status = "Rejected";
        booking.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Booking request was not approved by the administrator." : reason.Trim();
        booking.QrToken = null;
        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = $"Booking {id} rejected.";
        return RedirectToAction(nameof(BookingRequests));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Qr(string id)
    {
        FacilityBooking? booking = await db.FacilityBookings
            .Include(b => b.Facility)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();
        if (booking.Status != "Approved" && booking.Status != "Checked In")
        {
            TempData["FacilityError"] = "Only approved bookings can generate an Activity Check-In QR.";
            return RedirectToAction(nameof(BookingRequests));
        }

        if (string.IsNullOrWhiteSpace(booking.QrToken))
        {
            booking.QrToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await db.SaveChangesAsync();
        }

        string mobileUrl = Cloudflare.BuildPublicUrl($"/ActivityCheckIn/Index?token={Uri.EscapeDataString(booking.QrToken)}");
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(mobileUrl, QRCodeGenerator.ECCLevel.Q);
        using PngByteQRCode qr = new(data);
        ViewBag.QrDataUrl = "data:image/png;base64," + Convert.ToBase64String(qr.GetGraphic(12));
        ViewBag.MobileUrl = mobileUrl;
        return View(booking);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ActivityAttendance()
    {
        List<FacilityBooking> bookings = await db.FacilityBookings
            .AsNoTracking()
            .Include(b => b.Facility)
            .Include(b => b.User)
            .Include(b => b.Attendance)
            .Where(b => b.Status == "Approved" || b.Status == "Checked In")
            .OrderByDescending(b => b.BookingDate)
            .ThenBy(b => b.StartHour)
            .ToListAsync();
        return View(bookings);
    }

    private string GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("The login session is invalid.");

    private static DateTime MalaysiaNow()
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"); }
        catch { zone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
    }
}
