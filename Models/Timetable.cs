using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAndMobile.Models
{
    public class Timetable
    {
        [Key]
        public int TimetableId { get; set; }

        [ForeignKey(nameof(Course))]
        public int CourseId { get; set; }
        public Course? Course { get; set; }

        [ForeignKey(nameof(ClassGroup))]
        public int ClassId { get; set; }
        public ClassGroup? ClassGroup { get; set; }

        [ForeignKey(nameof(Classroom))]
        public int? ClassroomId { get; set; }
        public Classroom? Classroom { get; set; }

        public int TeacherId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public ClassType ClassType { get; set; }
    }
}