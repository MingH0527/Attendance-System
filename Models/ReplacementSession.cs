using System.ComponentModel.DataAnnotations; using System.ComponentModel.DataAnnotations.Schema; using Microsoft.EntityFrameworkCore;
namespace WebAndMobile.Models;
public class ReplacementSession {
 [Key,MaxLength(20)] public string ReplacementId {get;set;}=string.Empty;
 [MaxLength(20)] public string SourceTimetableId {get;set;}=string.Empty;
 [ForeignKey(nameof(SourceTimetableId)),DeleteBehavior(DeleteBehavior.Restrict)] public Timetable? SourceTimetable {get;set;}
 public DateTime? SourceOccurrenceDate {get;set;}
 public DateTime ReplacementDate {get;set;} public TimeSpan StartTime {get;set;} public TimeSpan EndTime {get;set;}
 public ClassType TargetClassType {get;set;} [MaxLength(20)] public string TeacherId {get;set;}=string.Empty;
 [MaxLength(20)] public string? ClassroomId {get;set;} [MaxLength(30)] public string Status {get;set;}="Scheduled";
 [MaxLength(150)] public string Reason {get;set;}="Replacement";
}
