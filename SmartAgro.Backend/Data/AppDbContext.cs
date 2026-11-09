using Microsoft.EntityFrameworkCore;
using SmartAgro.Shared;
using System;

namespace SmartAgro.Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Added Users table
        public DbSet<User> Users { get; set; }

        public DbSet<SensorReading> SensorReadings { get; set; }
        public DbSet<SensorConfig> Sensors { get; set; }
        public DbSet<SensorReadingHistory> SensorReadingsHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Optimize temporal and relational queries
            modelBuilder.Entity<SensorReading>()
                .HasIndex(r => r.CreatedAt);

            modelBuilder.Entity<SensorReading>()
                .HasIndex(r => r.SensorId);

            modelBuilder.Entity<SensorReadingHistory>()
                .HasIndex(h => h.CreatedAt);

            modelBuilder.Entity<User>()
            .HasMany(u => u.Sensors)
            .WithOne() // Leave this empty since SensorConfig no longer has the User property
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade); 
        }
    }
}