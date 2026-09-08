using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class FacilityImage
{
    [Key, MaxLength(20)]
    public string FacilityImageId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string FacilityId { get; set; } = string.Empty;
    public Facility? Facility { get; set; }

    [Required, MaxLength(255)]
    public string ImagePath { get; set; } = string.Empty;

    public bool IsCover { get; set; }
    public DateTime UploadedAt { get; set; }
}
