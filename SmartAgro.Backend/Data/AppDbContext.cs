using Microsoft.EntityFrameworkCore;
using SmartAgro.Shared;

namespace SmartAgro.Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

            modelBuilder.Entity<SensorConfig>().HasData(
                new SensorConfig { Id = 1, Name = "Теплиця 1 (Томати)" },
                new SensorConfig { Id = 2, Name = "Поле 3 (Пшениця)" }
            );
        }
    }
}