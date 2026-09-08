using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options)
        : base(options)
    {
    }

    public DbSet<AcademicProgram> AcademicPrograms => Set<AcademicProgram>();
    public DbSet<StudentClass> StudentClasses => Set<StudentClass>();
    public DbSet<TeacherCourse> TeacherCourses => Set<TeacherCourse>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Timetable> Timetables => Set<Timetable>();
    public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();
    public DbSet<PasskeyDeviceBinding> PasskeyDeviceBindings => Set<PasskeyDeviceBinding>();
    public DbSet<AdminSecurity> AdminSecurities => Set<AdminSecurity>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Teacher> Teacher => Set<Teacher>();
    public DbSet<TeacherProgram> TeacherPrograms => Set<TeacherProgram>();
    public DbSet<UserFaceProfile> UserFaceProfiles => Set<UserFaceProfile>();
    public DbSet<PaymentSecurity> PaymentSecurities => Set<PaymentSecurity>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<AcademicWeek> AcademicWeeks => Set<AcademicWeek>();
    public DbSet<AcademicCalendarEvent> AcademicCalendarEvents => Set<AcademicCalendarEvent>();
    public DbSet<ProgramCourse> ProgramCourses => Set<ProgramCourse>();
    public DbSet<ProgramSubjectHistory> ProgramSubjectHistories => Set<ProgramSubjectHistory>();
    public DbSet<TimetableException> TimetableExceptions => Set<TimetableException>();
    public DbSet<ReplacementSession> ReplacementSessions => Set<ReplacementSession>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<TimetableNotification> TimetableNotifications => Set<TimetableNotification>();
    public DbSet<TimetableAudit> TimetableAudits => Set<TimetableAudit>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityImage> FacilityImages => Set<FacilityImage>();
    public DbSet<FacilityBooking> FacilityBookings => Set<FacilityBooking>();
    public DbSet<FacilityAttendance> FacilityAttendances => Set<FacilityAttendance>();

    // ==============================
    // BARRED LIST
    // ==============================

    public DbSet<BarredStudent> BarredStudents =>
        Set<BarredStudent>();


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder
        );

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Phone)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<AcademicCalendarEvent>()
            .HasIndex(e =>
                new
                {
                    e.EventDate,
                    e.Name
                })
            .IsUnique();

        modelBuilder.Entity<TimetableAudit>()
            .HasIndex(a =>
                new
                {
                    a.TimetableId,
                    a.OccurrenceDate,
                    a.Status
                });

        modelBuilder.Entity<TimetableNotification>()
            .HasIndex(n =>
                new
                {
                    n.UserId,
                    n.IsRead,
                    n.CreatedAt
                });

        modelBuilder.Entity<ReplacementSession>()
            .HasIndex(r =>
                new
                {
                    r.SourceTimetableId,
                    r.SourceOccurrenceDate,
                    r.Status
                });

        modelBuilder.Entity<FacilityBooking>()
            .HasIndex(b => new { b.FacilityId, b.BookingDate, b.Status });

        modelBuilder.Entity<FacilityBooking>()
            .HasIndex(b => b.QrToken)
            .IsUnique()
            .HasFilter("[QrToken] IS NOT NULL");

        modelBuilder.Entity<FacilityBooking>()
            .HasOne(b => b.Attendance)
            .WithOne(a => a.Booking)
            .HasForeignKey<FacilityAttendance>(a => a.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FacilityAttendance>()
            .HasIndex(a => a.BookingId)
            .IsUnique();

        modelBuilder.Entity<FacilityAttendance>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FacilityAttendance>()
            .HasOne(a => a.Facility)
            .WithMany()
            .HasForeignKey(a => a.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Facility>().HasData(
            new Facility { FacilityId = "FAC001", FacilityName = "Computer Lab", Location = "Block A", Capacity = 30, Description = "A 24-hour self-service computer laboratory for student activities and independent work." },
            new Facility { FacilityId = "FAC002", FacilityName = "Gym", Location = "Sports Centre", Capacity = 30, Description = "A 24-hour self-service gym for student fitness and physical activities." },
            new Facility { FacilityId = "FAC003", FacilityName = "Swimming Pool", Location = "Pool Area", Capacity = 40, Description = "A 24-hour self-service swimming facility for approved student activities." },
            new Facility { FacilityId = "FAC004", FacilityName = "Sports Court", Location = "Sports Centre", Capacity = 40, Description = "A 24-hour self-service sports court for student recreational activities." },
            new Facility { FacilityId = "FAC005", FacilityName = "Music Room", Location = "Block B", Capacity = 20, Description = "A 24-hour self-service music room for student practice and activities." }
        );
    }
}