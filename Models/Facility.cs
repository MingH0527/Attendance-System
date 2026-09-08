using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class Facility
{
    [Key, MaxLength(20)]
    public string FacilityId { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string FacilityName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Location { get; set; } = string.Empty;

    public int Capacity { get; set; }

    [Required, MaxLength(600)]
    public string Description { get; set; } = string.Empty;

    public ICollection<FacilityImage> Images { get; set; } = new List<FacilityImage>();
    public ICollection<FacilityBooking> Bookings { get; set; } = new List<FacilityBooking>();
}
