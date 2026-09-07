using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace WebAndMobile.Models;
public class AcademicCalendarEvent {
 [Key,MaxLength(20)] public string CalendarEventId {get;set;}=string.Empty;
 [MaxLength(20)] public string? TermId {get;set;}
 [ForeignKey(nameof(TermId)),DeleteBehavior(DeleteBehavior.SetNull)] public AcademicTerm? Term {get;set;}
 public DateTime EventDate {get;set;} [Required,MaxLength(150)] public string Name {get;set;}=string.Empty;
 [MaxLength(30)] public string EventType {get;set;}="PublicHoliday"; public bool AffectsClasses {get;set;}=true;
}
