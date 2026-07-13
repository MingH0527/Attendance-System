using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Timetable> Timetables => Set<Timetable>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Phone)
            .IsUnique();

        modelBuilder.Entity<Course>()
            .HasIndex(c => c.CourseName)
            .IsUnique();

        modelBuilder.Entity<ClassGroup>()
            .HasIndex(c => c.ClassName)
            .IsUnique();

        modelBuilder.Entity<Classroom>()
            .HasIndex(c => c.RoomName)
            .IsUnique();

        modelBuilder.Entity<Timetable>()
            .HasOne(t => t.Course)
            .WithMany(c => c.Timetables)
            .HasForeignKey(t => t.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Timetable>()
            .HasOne(t => t.ClassGroup)
            .WithMany(c => c.Timetables)
            .HasForeignKey(t => t.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Timetable>()
            .HasOne(t => t.Classroom)
            .WithMany(c => c.Timetables)
            .HasForeignKey(t => t.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Timetable>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
