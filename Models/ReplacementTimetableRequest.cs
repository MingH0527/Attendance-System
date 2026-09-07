using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class ReplacementTimetableRequest
{
    [Required]
    [MaxLength(20)]
    public string SourceTimetableId { get; set; } =
        string.Empty;


    public DateTime SourceOccurrenceDate { get; set; }


    public DateTime ReplacementDate { get; set; }


    [Required]
    public string StartTime { get; set; } =
        string.Empty;


    [Required]
    public string EndTime { get; set; } =
        string.Empty;


    [MaxLength(20)]
    public string? ClassroomId { get; set; }


    [MaxLength(150)]
    public string? Reason { get; set; }
}