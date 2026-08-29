using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models
{
 
    public class AcademicProgram
    {
        [Key]
        [MaxLength(20)]
        public string ProgramId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string ProgramCode { get; set; } = ""; // e.g. "DIT"

        [Required]
        [MaxLength(150)]
        public string ProgramName { get; set; } = ""; // e.g. "Diploma in Information Technology"

        public ICollection<ClassGroup> Classes { get; set; } = new List<ClassGroup>();
    }
}