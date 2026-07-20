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

    public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();

    public DbSet<AdminSecurity> AdminSecurities =>
        Set<AdminSecurity>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User unique fields
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Phone)
            .IsUnique();

        // Course unique name
        modelBuilder.Entity<Course>()
            .HasIndex(c => c.CourseName)
            .IsUnique();

        // Class group unique name
        modelBuilder.Entity<ClassGroup>()
            .HasIndex(c => c.ClassName)
            .IsUnique();

        // Classroom unique name
        modelBuilder.Entity<Classroom>()
            .HasIndex(c => c.RoomName)
            .IsUnique();

        // Timetable -> Course
        modelBuilder.Entity<Timetable>()
            .HasOne(t => t.Course)
            .WithMany(c => c.Timetables)
            .HasForeignKey(t => t.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Timetable -> ClassGroup
        modelBuilder.Entity<Timetable>()
            .HasOne(t => t.ClassGroup)
            .WithMany(c => c.Timetables)
            .HasForeignKey(t => t.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        // Timetable -> Classroom
        modelBuilder.Entity<Timetable>()
            .HasOne(t => t.Classroom)
            .WithMany(c => c.Timetables)
            .HasForeignKey(t => t.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Timetable -> Teacher
        modelBuilder.Entity<Timetable>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // CredentialId cannot be duplicated
        modelBuilder.Entity<UserPasskey>()
            .HasIndex(p => p.CredentialId)
            .IsUnique();

        // One user can have many passkeys
        modelBuilder.Entity<UserPasskey>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdminSecurity>()
            .HasIndex(a => a.UserId)
            .IsUnique();

        modelBuilder.Entity<AdminSecurity>()
            .HasOne(a => a.User)
            .WithOne(u => u.AdminSecurity)
            .HasForeignKey<AdminSecurity>(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One student and one teacher can only have one conversation.
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => new { c.StudentId, c.TeacherId })
            .IsUnique();

        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.Student)
            .WithMany()
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => c.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.ConversationId, m.ChatMessageId });

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.ReceiverId, m.IsRead });

    }
}