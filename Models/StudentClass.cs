using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models
{
    public class StudentClass
    {
        [Key]
        public int StudentClassId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

     
        [Required]
        [MaxLength(20)]
        public string StudentId { get; set; } = "";

        [ForeignKey(nameof(Program))]
        public int ProgramId { get; set; }
        public AcademicProgram? Program { get; set; }

        [ForeignKey(nameof(ClassGroup))]
        public int ClassId { get; set; }
        public ClassGroup? ClassGroup { get; set; }

        public DateTime RegisteredDate { get; set; } = DateTime.Now;
    }
}