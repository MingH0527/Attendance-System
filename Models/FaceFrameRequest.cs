using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models;

public class FaceFrameRequest
{
    [Required]
    public string ImageDataUrl { get; set; } = "";

    public bool IsStable { get; set; } = true;
}
