using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models
{
    public class ProgramRegisterViewModel
    {
        public string Position { get; set; } = ""; 

        public List<ClassGroup> AvailableClasses { get; set; } = new(); // student use
        public List<Course> AvailableCourses { get; set; } = new();     // teacher use

        // for student
        public int? SelectedClassId { get; set; }

        // for teacher
        public List<int> SelectedCourseIds { get; set; } = new();

        public string? CurrentClassName { get; set; }
        public List<string> CurrentCourseNames { get; set; } = new();
    }
}