using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models
{
    public class StudentClass
    {
        [Key]
        public int RegistrationId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }
      
        [ForeignKey(nameof(ClassGroup))]
        public int ClassId { get; set; }
        public ClassGroup? ClassGroup { get; set; }

        public DateTime RegisteredDate { get; set; } = DateTime.Now;
    }
}