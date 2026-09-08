namespace WebAndMobile.Models;

public class FacilityBookingPageViewModel
{
    public Facility Facility { get; set; } = new();
    public DateTime SelectedDate { get; set; }
    public DateTime Today { get; set; }
    public HashSet<int> UnavailableStartHours { get; set; } = new();
    public HashSet<int> OwnBookingStartHours { get; set; } = new();
    public int ActiveBookingCount { get; set; }
    public int ActiveBookingLimit { get; set; } = 2;
    public bool BookingLimitReached => ActiveBookingCount >= ActiveBookingLimit;
}

public class FacilityBookingRequestViewModel
{
    public string FacilityId { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }
}

public class ActivityCheckInLoginViewModel
{
    public string Token { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
