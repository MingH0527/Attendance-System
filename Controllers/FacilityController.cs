using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebAndMobile.Configuration;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

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
        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);
        string currentUserId = GetCurrentUserId();
        ViewBag.ActiveBookingCount = await GetActiveBookingCount(currentUserId, now);

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
        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);

        Facility? facility = await db.Facilities
            .AsNoTracking()
            .Include(f => f.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.UploadedAt))
            .FirstOrDefaultAsync(f => f.FacilityId == id);

        if (facility == null) return NotFound();

        DateTime today = now.Date;
        DateTime selected = (date ?? today).Date;
        if (selected < today || selected > today.AddDays(7)) selected = today;

        List<FacilityBooking> occupiedBookings = await db.FacilityBookings
            .AsNoTracking()
            .Where(b => b.FacilityId == id &&
                        b.BookingDate == selected &&
                        (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In"))
            .ToListAsync();

        occupiedBookings = occupiedBookings
            .Where(b => GetEndDateTime(b) > now)
            .ToList();

        HashSet<int> unavailable = occupiedBookings
            .SelectMany(b => Enumerable.Range(b.StartHour / 2, (b.EndHour - b.StartHour) / 2))
            .Select(x => x * 2)
            .ToHashSet();

        string currentUserId = GetCurrentUserId();
        List<FacilityBooking> ownBookings = occupiedBookings
            .Where(b => b.UserId == currentUserId)
            .ToList();

        HashSet<int> ownBookingStartHours = ownBookings
            .SelectMany(b => Enumerable.Range(b.StartHour / 2, (b.EndHour - b.StartHour) / 2))
            .Select(x => x * 2)
            .ToHashSet();

        int activeBookingCount = await GetActiveBookingCount(currentUserId, now);

        return View(new FacilityBookingPageViewModel
        {
            Facility = facility,
            SelectedDate = selected,
            Today = today,
            UnavailableStartHours = unavailable,
            OwnBookingStartHours = ownBookingStartHours,
            ActiveBookingCount = activeBookingCount
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

        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);

        DateTime requestedStartDateTime = date.AddHours(model.StartHour);
        if (requestedStartDateTime <= now)
        {
            TempData["FacilityError"] = "The selected booking start time has already passed. Please choose a future time slot.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        string currentUserId = GetCurrentUserId();
        int activeBookingCount = await GetActiveBookingCount(currentUserId, now);
        if (activeBookingCount >= 2)
        {
            TempData["FacilityError"] = "Booking Limit Reached. You currently have 2 active facility bookings. Complete or cancel an existing booking before making another booking.";
            return RedirectToAction(nameof(Details), new { id = model.FacilityId, date = date.ToString("yyyy-MM-dd") });
        }

        List<FacilityBooking> possibleCollisions = await db.FacilityBookings
            .AsNoTracking()
            .Where(b => b.FacilityId == model.FacilityId &&
                        b.BookingDate == date &&
                        (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In") &&
                        model.StartHour < b.EndHour && model.EndHour > b.StartHour)
            .ToListAsync();

        bool collision = possibleCollisions.Any(b => GetEndDateTime(b) > now);

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
        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);
        string userId = GetCurrentUserId();
        ViewBag.ActiveBookingCount = await GetActiveBookingCount(userId, now);
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

        if (booking.Status is not ("Pending" or "Approved") || GetEndDateTime(booking) <= MalaysiaNow())
        {
            TempData["FacilityError"] = "Only active pending or approved bookings can be cancelled before activity check-in.";
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
    public async Task<IActionResult> Analytics()
    {
        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);

        List<FacilityBooking> bookings = await db.FacilityBookings
            .AsNoTracking()
            .Include(b => b.Facility)
            .Include(b => b.User)
            .Include(b => b.Attendance)
            .OrderBy(b => b.BookingDate)
            .ThenBy(b => b.StartHour)
            .ToListAsync();

        List<Facility> facilities = await db.Facilities
            .AsNoTracking()
            .OrderBy(f => f.FacilityId)
            .ToListAsync();

        FacilityAnalyticsViewModel model = BuildFacilityAnalytics(bookings, facilities, now);
        return View(model);
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
        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);
        ViewBag.MalaysiaNow = now;

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
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminCancel(string id)
    {
        FacilityBooking? booking = await db.FacilityBookings.FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound();

        DateTime now = MalaysiaNow();
        if (booking.Status is not ("Pending" or "Approved") || GetEndDateTime(booking) <= now)
        {
            TempData["FacilityError"] = "Only active Pending or Approved bookings can be cancelled before the student checks in.";
            return RedirectToAction(nameof(BookingRequests));
        }

        booking.Status = "Cancelled";
        booking.QrToken = null;
        await db.SaveChangesAsync();
        TempData["FacilitySuccess"] = $"Booking {id} cancelled. The facility slot and the student's active-booking quota were released immediately.";
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
        DateTime now = MalaysiaNow();
        DateTime startDateTime = GetStartDateTime(booking);
        DateTime endDateTime = GetEndDateTime(booking);

        if (booking.Status != "Approved" || now < startDateTime || now >= endDateTime)
        {
            TempData["FacilityError"] = now < startDateTime
                ? $"Activity QR is not available yet. It becomes available at {startDateTime:dd MMM yyyy hh:mm tt}."
                : "Activity QR has expired because the booking end time has been reached.";
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
        DateTime now = MalaysiaNow();
        await CompleteEndedActiveBookings(now);

        List<FacilityBooking> bookings = await db.FacilityBookings
            .AsNoTracking()
            .Include(b => b.Facility)
            .Include(b => b.User)
            .Include(b => b.Attendance)
            .Where(b => b.Status == "Approved" || b.Status == "Checked In" || b.Status == "Completed")
            .OrderByDescending(b => b.BookingDate)
            .ThenBy(b => b.StartHour)
            .ToListAsync();
        return View(bookings);
    }

    private async Task<int> GetActiveBookingCount(string userId, DateTime now)
    {
        List<FacilityBooking> candidates = await db.FacilityBookings
            .AsNoTracking()
            .Where(b => b.UserId == userId &&
                        (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In"))
            .ToListAsync();

        return candidates.Count(b => GetEndDateTime(b) > now);
    }

    private async Task CompleteEndedActiveBookings(DateTime now)
    {
        List<FacilityBooking> ended = await db.FacilityBookings
            .Where(b => b.Status == "Pending" || b.Status == "Approved" || b.Status == "Checked In")
            .ToListAsync();

        ended = ended.Where(b => GetEndDateTime(b) <= now).ToList();
        if (ended.Count == 0) return;

        foreach (FacilityBooking booking in ended)
        {
            booking.Status = "Completed";
        }

        await db.SaveChangesAsync();
    }

    private static FacilityAnalyticsViewModel BuildFacilityAnalytics(
        List<FacilityBooking> bookings,
        List<Facility> facilities,
        DateTime now)
    {
        FacilityAnalyticsViewModel model = new()
        {
            TotalBookings = bookings.Count,
            TotalFacilities = facilities.Count
        };

        // Booking hours represent facility time reserved by bookings that were
        // not cancelled or rejected. Pending bookings are included because they
        // still block the facility slot while awaiting a decision.
        List<FacilityBooking> booked = bookings
            .Where(b => b.Status is not ("Cancelled" or "Rejected"))
            .ToList();

        model.TotalBookingHours = booked.Sum(b => Math.Max(0, b.EndHour - b.StartHour));
        model.CancellationRate = bookings.Count == 0
            ? 0
            : Math.Round(bookings.Count(b => b.Status == "Cancelled") * 100d / bookings.Count, 1);

        // Completion rate uses only bookings that already have a final outcome.
        // Pending, Approved and currently Checked In bookings are excluded from
        // the denominator because they have not reached their final outcome yet.
        int completedCount = bookings.Count(b => b.Status == "Completed");
        int cancelledCount = bookings.Count(b => b.Status == "Cancelled");
        int rejectedCount = bookings.Count(b => b.Status == "Rejected");
        int finalOutcomeCount = completedCount + cancelledCount + rejectedCount;

        model.CompletionRate = finalOutcomeCount == 0
            ? 0
            : Math.Round(completedCount * 100d / finalOutcomeCount, 1);

        // Attendance outcome still uses only approved bookings whose booked time
        // has ended. This is independent from the current booking lifecycle status.
        List<FacilityBooking> completedApproved = bookings
            .Where(b => b.ApprovedAt != null &&
                        b.Status is not ("Cancelled" or "Rejected") &&
                        GetEndDateTime(b) <= now)
            .ToList();

        int checkedInCompleted = completedApproved.Count(b => b.Attendance != null);

        DateTime today = now.Date;

        // 1. Last 7 days booking hours.
        for (int back = 6; back >= 0; back--)
        {
            DateTime day = today.AddDays(-back);
            double hours = booked
                .Where(b => b.BookingDate.Date == day)
                .Sum(b => Math.Max(0, b.EndHour - b.StartHour));

            model.Last7DaysBookingHours.Add(new FacilityAnalyticsPoint
            {
                Label = day.ToString("ddd dd"),
                Value = hours,
                Count = booked.Count(b => b.BookingDate.Date == day)
            });
        }

        // 2. Monthly booking hours - current calendar year.
        for (int monthNumber = 1; monthNumber <= 12; monthNumber++)
        {
            DateTime month = new(now.Year, monthNumber, 1);
            double hours = booked
                .Where(b => b.BookingDate.Year == month.Year && b.BookingDate.Month == month.Month)
                .Sum(b => Math.Max(0, b.EndHour - b.StartHour));

            model.MonthlyBookingHours.Add(new FacilityAnalyticsPoint
            {
                Label = month.ToString("MMM"),
                Value = hours,
                Count = booked.Count(b => b.BookingDate.Year == month.Year && b.BookingDate.Month == month.Month)
            });
        }

        // Work out the monthly range once and reuse it for all long-term charts.
        DateTime firstMonth = bookings.Count == 0
            ? new DateTime(now.Year, now.Month, 1)
            : new DateTime(bookings.Min(b => b.BookingDate).Year, bookings.Min(b => b.BookingDate).Month, 1);
        DateTime currentMonth = new(now.Year, now.Month, 1);

        List<DateTime> allMonths = [];
        for (DateTime month = firstMonth; month <= currentMonth; month = month.AddMonths(1))
        {
            allMonths.Add(month);
        }

        // 3. All-time usage trend.
        foreach (DateTime month in allMonths)
        {
            List<FacilityBooking> rows = booked
                .Where(b => b.BookingDate.Year == month.Year && b.BookingDate.Month == month.Month)
                .ToList();

            model.AllTimeUsageTrend.Add(new FacilityAnalyticsPoint
            {
                Label = month.ToString("MMM yy"),
                Value = rows.Sum(b => Math.Max(0, b.EndHour - b.StartHour)),
                Count = rows.Count
            });
        }

        // 4. Current/final booking status distribution.
        string[] statuses = ["Pending", "Approved", "Checked In", "Completed", "Cancelled", "Rejected"];
        foreach (string status in statuses)
        {
            model.BookingStatusDistribution.Add(new FacilityAnalyticsPoint
            {
                Label = status,
                Value = bookings.Count(b => b.Status == status),
                Count = bookings.Count(b => b.Status == status)
            });
        }

        // 5. Booking outcome/status trend by booking month (last 6 months).
        List<DateTime> recentSixMonths = [];
        for (int back = 5; back >= 0; back--)
        {
            recentSixMonths.Add(currentMonth.AddMonths(-back));
        }

        model.BookingStatusTrend.Labels = recentSixMonths.Select(m => m.ToString("MMM yy")).ToList();
        foreach (string status in statuses)
        {
            model.BookingStatusTrend.Series.Add(new FacilityAnalyticsSeries
            {
                Name = status,
                Values = recentSixMonths
                    .Select(m => (double)bookings.Count(b =>
                        b.BookingDate.Year == m.Year &&
                        b.BookingDate.Month == m.Month &&
                        b.Status == status))
                    .ToList()
            });
        }

        // 6. Check-in vs no-show. Only approved bookings that have ended are eligible.
        int noShow = completedApproved.Count - checkedInCompleted;
        model.CheckInVsNoShow =
        [
            new FacilityAnalyticsPoint { Label = "Checked In", Value = checkedInCompleted, Count = checkedInCompleted },
            new FacilityAnalyticsPoint { Label = "No Show", Value = noShow, Count = noShow }
        ];

        // 7. Peak booking hours. The booking module works in 2-hour blocks.
        for (int hour = 0; hour < 24; hour += 2)
        {
            int count = booked.Count(b => b.StartHour <= hour && b.EndHour > hour);
            model.PeakBookingHours.Add(new FacilityAnalyticsPoint
            {
                Label = $"{FormatHour(hour)}-{FormatHour(hour + 2)}",
                Value = count,
                Count = count
            });
        }

        FacilityAnalyticsPoint? peakSlot = model.PeakBookingHours
            .OrderByDescending(p => p.Value)
            .FirstOrDefault();
        if (peakSlot != null && peakSlot.Value > 0)
        {
            model.PeakBookingSlot = $"{peakSlot.Label} · {peakSlot.Count} bookings";
        }

        // 8. Booking duration distribution. Current validation allows 2, 4 or 6 hours.
        int[] durations = [2, 4, 6];
        foreach (int hours in durations)
        {
            int count = bookings.Count(b => b.EndHour - b.StartHour == hours);
            model.BookingDurationDistribution.Add(new FacilityAnalyticsPoint
            {
                Label = $"{hours} hours",
                Value = count,
                Count = count
            });
        }

        // 9, 10 and 11. Facility hours, top 3 and booking share.
        foreach (Facility facility in facilities)
        {
            List<FacilityBooking> facilityBookings = booked
                .Where(b => b.FacilityId == facility.FacilityId)
                .ToList();

            double hours = facilityBookings.Sum(b => Math.Max(0, b.EndHour - b.StartHour));

            model.FacilityUsageHours.Add(new FacilityAnalyticsPoint
            {
                Label = facility.FacilityName,
                Value = hours,
                Count = facilityBookings.Count
            });

            model.BookingShareByFacility.Add(new FacilityAnalyticsPoint
            {
                Label = facility.FacilityName,
                Value = facilityBookings.Count,
                Count = facilityBookings.Count
            });
        }

        model.TopFacilities = facilities
            .Select(f =>
            {
                List<FacilityBooking> rows = booked.Where(b => b.FacilityId == f.FacilityId).ToList();
                return new FacilityAnalyticsRank
                {
                    Label = f.FacilityName,
                    Count = rows.Count,
                    Hours = rows.Sum(b => Math.Max(0, b.EndHour - b.StartHour))
                };
            })
            .OrderByDescending(r => r.Count)
            .ThenByDescending(r => r.Hours)
            .Take(3)
            .ToList();

        if (model.TopFacilities.Count > 0 && model.TopFacilities[0].Count > 0)
        {
            model.MostBookedFacility = $"{model.TopFacilities[0].Label} · {model.TopFacilities[0].Count} bookings";
        }

        // 12. Five/all facilities - last 7 days comparison.
        List<DateTime> lastSevenDays = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();
        model.FacilityLast7DaysComparison.Labels = lastSevenDays.Select(d => d.ToString("ddd dd")).ToList();
        foreach (Facility facility in facilities)
        {
            model.FacilityLast7DaysComparison.Series.Add(new FacilityAnalyticsSeries
            {
                Name = facility.FacilityName,
                Values = lastSevenDays
                    .Select(d => (double)booked.Count(b => b.FacilityId == facility.FacilityId && b.BookingDate.Date == d))
                    .ToList()
            });
        }

        // 13. Facility monthly comparison - last 6 calendar months.
        model.FacilityMonthlyComparison.Labels = recentSixMonths.Select(m => m.ToString("MMM yy")).ToList();
        foreach (Facility facility in facilities)
        {
            model.FacilityMonthlyComparison.Series.Add(new FacilityAnalyticsSeries
            {
                Name = facility.FacilityName,
                Values = recentSixMonths
                    .Select(m => (double)booked.Count(b =>
                        b.FacilityId == facility.FacilityId &&
                        b.BookingDate.Year == m.Year &&
                        b.BookingDate.Month == m.Month))
                    .ToList()
            });
        }

        // 14. All-time facility trend.
        model.FacilityAllTimeTrend.Labels = allMonths.Select(m => m.ToString("MMM yy")).ToList();
        foreach (Facility facility in facilities)
        {
            model.FacilityAllTimeTrend.Series.Add(new FacilityAnalyticsSeries
            {
                Name = facility.FacilityName,
                Values = allMonths
                    .Select(m => (double)booked.Count(b =>
                        b.FacilityId == facility.FacilityId &&
                        b.BookingDate.Year == m.Year &&
                        b.BookingDate.Month == m.Month))
                    .ToList()
            });
        }

        // 15. Top facility users. Facility booking is currently Student-only,
        // so this correctly ranks students rather than presenting a fake
        // Student-vs-Teacher split that would always be 100% Student.
        model.TopStudents = booked
            .GroupBy(b => new
            {
                b.UserId,
                Name = string.IsNullOrWhiteSpace(b.User?.Username) ? b.UserId : b.User!.Username
            })
            .Select(g => new FacilityAnalyticsRank
            {
                Label = g.Key.Name,
                Count = g.Count(),
                Hours = g.Sum(b => Math.Max(0, b.EndHour - b.StartHour))
            })
            .OrderByDescending(r => r.Hours)
            .ThenByDescending(r => r.Count)
            .Take(3)
            .ToList();

        if (model.TopStudents.Count > 0 && model.TopStudents[0].Hours > 0)
        {
            model.TopFacilityUser = $"{model.TopStudents[0].Label} · {model.TopStudents[0].Hours:0.#} hrs";
        }

        // 16. Booking lead time. This replaces Student-vs-Teacher because the
        // current booking action is Authorize(Roles = "Student"), so that pie
        // would contain no useful comparison.
        int sameDay = 0;
        int oneTwo = 0;
        int threeFour = 0;
        int fiveSeven = 0;

        foreach (FacilityBooking booking in bookings)
        {
            int leadDays = Math.Max(0, (booking.BookingDate.Date - booking.CreatedAt.Date).Days);
            if (leadDays == 0) sameDay++;
            else if (leadDays <= 2) oneTwo++;
            else if (leadDays <= 4) threeFour++;
            else fiveSeven++;
        }

        model.BookingLeadTimeDistribution =
        [
            new FacilityAnalyticsPoint { Label = "Same day", Value = sameDay, Count = sameDay },
            new FacilityAnalyticsPoint { Label = "1-2 days", Value = oneTwo, Count = oneTwo },
            new FacilityAnalyticsPoint { Label = "3-4 days", Value = threeFour, Count = threeFour },
            new FacilityAnalyticsPoint { Label = "5-7 days", Value = fiveSeven, Count = fiveSeven }
        ];

        return model;
    }

    private static string FormatHour(int hour)
    {
        int normalized = ((hour % 24) + 24) % 24;
        DateTime time = DateTime.Today.AddHours(normalized);
        return time.ToString("htt").ToLowerInvariant();
    }

    private static DateTime GetStartDateTime(FacilityBooking booking) =>
        booking.BookingDate.Date.AddHours(booking.StartHour);

    private static DateTime GetEndDateTime(FacilityBooking booking) =>
        booking.BookingDate.Date.AddHours(booking.EndHour);

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
