using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models;

public class Teacher
{
    [Key]
    public int TeacherId { get; set; }

    [Required]
    public int UserId {  get; set; }

    [Required]
    [MaxLength(100)]
    public string Department { get; set; } = "";

    [Required]
    public decimal Salary { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public DateTime HireDate { get; set; }




}



