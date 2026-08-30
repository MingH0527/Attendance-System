using System.ComponentModel.DataAnnotations; using System.ComponentModel.DataAnnotations.Schema; using Microsoft.EntityFrameworkCore;
namespace WebAndMobile.Models;
public class AcademicWeek {
 [Key,MaxLength(20)] public string AcademicWeekId {get;set;}=string.Empty;
 [MaxLength(20)] public string TermId {get;set;}=string.Empty;
 [ForeignKey(nameof(TermId)),DeleteBehavior(DeleteBehavior.Cascade)] public AcademicTerm? Term {get;set;}
 public int WeekNumber {get;set;} public DateTime StartDate {get;set;} public DateTime EndDate {get;set;}
 [MaxLength(30)] public string WeekType {get;set;}="Teaching";
}
