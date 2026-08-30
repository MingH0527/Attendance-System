using System.ComponentModel.DataAnnotations; using System.ComponentModel.DataAnnotations.Schema; using Microsoft.EntityFrameworkCore;
namespace WebAndMobile.Models;
public class ClassSession {
 [Key,MaxLength(20)] public string SessionId {get;set;}=string.Empty;
 [MaxLength(20)] public string TermId {get;set;}=string.Empty;
 [MaxLength(20)] public string TimetableId {get;set;}=string.Empty;
 [ForeignKey(nameof(TimetableId)),DeleteBehavior(DeleteBehavior.Restrict)] public Timetable? Timetable {get;set;}
 [MaxLength(20)] public string? ReplacementId {get;set;} public DateTime SessionDate {get;set;}
 public TimeSpan StartTime {get;set;} public TimeSpan EndTime {get;set;}
 [MaxLength(30)] public string Status {get;set;}="Scheduled"; [MaxLength(100)] public string? Reason {get;set;}
}
