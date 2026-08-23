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

            // Seed a default user
            var defaultUserId = Guid.Parse("d8a7a8d8-a8d8-4a8d-b8d8-d8a7a8d8a7a8");
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = defaultUserId,
                Name = "Darya",
                AverageCycleLength = 28,
                AveragePeriodLength = 5,
                LastPeriodStartDate = DateTime.UtcNow.Date.AddDays(-10), // Default start date 10 days ago (Follicular phase)
                PartnerTelegramChatId = null,
                Language = "fa"
            });
        }
    }
}
