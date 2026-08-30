using System.ComponentModel.DataAnnotations;
namespace WebAndMobile.Models;
public class AcademicTerm {
 [Key,MaxLength(20)] public string TermId {get;set;}=string.Empty;
 [Required,MaxLength(100)] public string Name {get;set;}=string.Empty;
 public DateTime StartDate {get;set;} public DateTime EndDate {get;set;}
 [Required,MaxLength(20)] public string Status {get;set;}="Upcoming";
 public ICollection<AcademicWeek> Weeks {get;set;}=new List<AcademicWeek>();
 public ICollection<Timetable> Timetables {get;set;}=new List<Timetable>();
}
