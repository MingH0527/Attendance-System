using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models
{
    public class ProgramRegisterViewModel
    {
        public string Position { get; set; } = "";

        public List<AcademicProgram> AvailablePrograms { get; set; } = new(); 
        public List<ClassGroup> AvailableClasses { get; set; } = new();       
        public List<Course> AvailableCourses { get; set; } = new();       

       
        [Required(ErrorMessage = "Please select a program")]
        public int SelectedProgramId { get; set; }
        public int? SelectedClassId { get; set; }

        public List<int> SelectedCourseIds { get; set; } = new();

        public string? CurrentStudentId { get; set; }
        public string? CurrentProgramName { get; set; }
        public string? CurrentClassName { get; set; }
        public List<string> CurrentCourseNames { get; set; } = new();
    }
}