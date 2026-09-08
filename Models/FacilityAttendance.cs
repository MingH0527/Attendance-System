using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class FacilityAttendance
{
    [Key, MaxLength(20)]
    public string FacilityAttendanceId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string BookingId { get; set; } = string.Empty;
    public FacilityBooking? Booking { get; set; }

    [Required, MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    [Required, MaxLength(20)]
    public string FacilityId { get; set; } = string.Empty;
    public Facility? Facility { get; set; }

    public DateTime CheckInTime { get; set; }
    public bool QrVerified { get; set; }
    public bool FaceVerified { get; set; }

    [Required, MaxLength(50)]
    public string VerificationMethod { get; set; } = "Student Login + QR + Face ID";

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Present";
}
