using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class SetPaymentPinViewModel
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN must contain exactly 6 digits.")]
    public string Pin { get; set; } = "";

    [Required]
    [Compare(nameof(Pin), ErrorMessage = "PIN and Confirm PIN do not match.")]
    public string ConfirmPin { get; set; } = "";

    public int ReturnFeeId { get; set; }
}
