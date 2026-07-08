using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}