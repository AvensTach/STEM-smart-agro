using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;

namespace SmartAgro.Backend.Services
{
    public class DataArchiverService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public DataArchiverService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Беремо дані, яким більше 2 днів
                var cutoffDate = DateTime.UtcNow.AddDays(-2);

                var oldReadings = await context.SensorReadings
                    .Where(r => r.CreatedAt < cutoffDate)
                    .ToListAsync(stoppingToken);

                if (oldReadings.Any())
                {
                    // Групуємо по сенсору, дню та годині
                    var historyRecords = oldReadings
                        .GroupBy(r => new { r.SensorId, r.CreatedAt.Date, r.CreatedAt.Hour })
                        .Select(g => new SensorReadingHistory
                        {
                            SensorId = g.Key.SensorId,
                            Temperature = Math.Round(g.Average(x => x.Temperature), 1),
                            SoilHumidity = Math.Round(g.Average(x => x.SoilHumidity), 1),
                            LightLevel = Math.Round(g.Average(x => x.LightLevel), 0),
                            CreatedAt = new DateTime(g.Key.Date.Year, g.Key.Date.Month, g.Key.Date.Day, g.Key.Hour, 0, 0, DateTimeKind.Utc)
                        }).ToList();

                    // Зберігаємо стиснуті дані
                    context.SensorReadingsHistory.AddRange(historyRecords);
                    
                    // Видаляємо сирі дані
                    context.SensorReadings.RemoveRange(oldReadings);
                    
                    await context.SaveChangesAsync(stoppingToken);
                }

                // Перевіряємо базу кожні 12 годин
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }
    }
}