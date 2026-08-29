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
    public DbSet<UserFaceProfile> UserFaceProfiles => Set<UserFaceProfile>();
    public DbSet<PaymentSecurity> PaymentSecurities => Set<PaymentSecurity>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
}
