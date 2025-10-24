using DeviceManagementSolution.Domain.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class DeviceDbContext : DbContext
    {
        public DeviceDbContext(DbContextOptions<DeviceDbContext> options) : base(options) { }

        public DbSet<Device> Devices { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<DeviceStatsDummy> DeviceStatsDummy { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Soft delete filter
            modelBuilder.Entity<Device>().HasQueryFilter(d => !d.IsDeleted);

            // Notification -> UserNotification relationship
            modelBuilder.Entity<UserNotification>()
                .HasOne(un => un.Notification)
                .WithMany(n => n.UserNotifications)
                .HasForeignKey(un => un.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional: unique index per user & notification to prevent duplicates
            modelBuilder.Entity<UserNotification>()
                .HasIndex(un => new { un.UserId, un.NotificationId })
                .IsUnique();

            modelBuilder.Entity<DeviceStatsDummy>().ToTable("DeviceStatsDummy");


            base.OnModelCreating(modelBuilder);

        }
    }
}
