using Microsoft.EntityFrameworkCore;
using Darya.Server.Models;

namespace Darya.Server.Data
{
    public class DaryaDbContext : DbContext
    {
        public DaryaDbContext(DbContextOptions<DaryaDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<DailyLog> DailyLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique index on Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Unique index on PartnerCode (allows multiple nulls in PostgreSQL)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.PartnerCode)
                .IsUnique();

            // Strict foreign key from DailyLog to User
            modelBuilder.Entity<DailyLog>()
                .HasOne(dl => dl.User)
                .WithMany(u => u.DailyLogs)
                .HasForeignKey(dl => dl.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
