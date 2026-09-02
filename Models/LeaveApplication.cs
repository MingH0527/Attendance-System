using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class LeaveApplication
{
    [Key]
    public int LeaveId { get; set; }

    [Required]
    [MaxLength(20)]
    public string StudentId { get; set; } = "";

    [Required(ErrorMessage = "Please select a leave type.")]
    [MaxLength(50)]
    public string LeaveType { get; set; } = "";

    [Required(ErrorMessage = "Please select a start date.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "Please select an end date.")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Required(ErrorMessage = "Please enter a reason.")]
    [MaxLength(500)]
    public string Reason { get; set; } = "";

    [MaxLength(255)]
    public string? SupportingDocument { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime AppliedAt { get; set; } = DateTime.Now;

    [ForeignKey(nameof(StudentId))]
    public User? Student { get; set; }
}