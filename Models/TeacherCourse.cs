using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models
{
    // 单纯声明"这位老师教这门课", 跟 Timetable 的具体排课时段无关(那部分仍由管理员另外安排)
    public class TeacherCourse
    {
        [Key]
        public int TeacherCourseId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        [ForeignKey(nameof(Course))]
        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public DateTime RegisteredDate { get; set; } = DateTime.Now;
    }
}