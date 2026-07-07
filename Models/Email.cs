using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class EmailModel
{
	[Required(ErrorMessage = "Email is required.")]
	[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
	public string Email { get; set; } = "";

	[Required(ErrorMessage = "Message is required.")]
	public string Message { get; set; } = "";
}