using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class ApplyLeaveViewModel
{
    [Required(ErrorMessage = "Please select a leave type.")]
    public string LeaveType { get; set; } = "";

    [Required(ErrorMessage = "Please select a start date.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Please select an end date.")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Please enter your reason.")]
    [MaxLength(500)]
    public string Reason { get; set; } = "";

    public IFormFile? SupportingDocument { get; set; }

    public List<LeaveApplication> LeaveHistory { get; set; } = new();
}