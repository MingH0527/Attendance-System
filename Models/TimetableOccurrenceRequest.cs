using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class TimetableOccurrenceRequest
{
    [Required]
    [MaxLength(20)]
    public string TimetableId { get; set; } =
        string.Empty;


    public DateTime OccurrenceDate { get; set; }


    [Required]
    [MaxLength(30)]
    public string Status { get; set; } =
        "Normal";


    [MaxLength(20)]
    public string? ClassroomId { get; set; }


    public string? StartTime { get; set; }


    public string? EndTime { get; set; }


    [MaxLength(250)]
    public string? Reason { get; set; }
}