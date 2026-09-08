using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class FacilityBooking
{
    [Key, MaxLength(20)]
    public string BookingId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    [Required, MaxLength(20)]
    public string StudentId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string FacilityId { get; set; } = string.Empty;
    public Facility? Facility { get; set; }

    public DateTime BookingDate { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(20)]
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(300)]
    public string? RejectionReason { get; set; }

    [MaxLength(128)]
    public string? QrToken { get; set; }

    public DateTime CreatedAt { get; set; }
    public FacilityAttendance? Attendance { get; set; }
}
