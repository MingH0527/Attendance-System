using System.ComponentModel.DataAnnotations;

namespace WebAndMobile.Models
{
    public class ProgramRegisterViewModel
    {
        public string Position { get; set; } = ""; // "Student" or "Teacher", 从登录用户带过来, 用来决定显示哪个表单

        public List<ClassGroup> AvailableClasses { get; set; } = new(); // 学生用
        public List<Course> AvailableCourses { get; set; } = new();     // 老师用

        // 学生专用
        public int? SelectedClassId { get; set; }

        // 老师专用 (可以教多门课)
        public List<int> SelectedCourseIds { get; set; } = new();

        // 如果已经注册过, 用来在页面上显示目前状态
        public string? CurrentClassName { get; set; }
        public List<string> CurrentCourseNames { get; set; } = new();
    }
}