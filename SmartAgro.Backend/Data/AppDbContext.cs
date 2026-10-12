using Microsoft.EntityFrameworkCore;
using SmartAgro.Shared;

namespace SmartAgro.Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Ця властивість представляє таблицю в базі даних
        public DbSet<SensorReading> SensorReadings { get; set; }
    }
}