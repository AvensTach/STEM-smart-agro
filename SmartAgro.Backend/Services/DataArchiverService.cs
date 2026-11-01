using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;
using Microsoft.Extensions.Logging;

namespace SmartAgro.Backend.Services
{
    public class DataArchiverService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataArchiverService> _logger;

        public DataArchiverService(IServiceProvider serviceProvider, ILogger<DataArchiverService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Fetch readings older than 2 days
                var cutoffDate = DateTime.UtcNow.AddDays(-2);

                var oldReadings = await context.SensorReadings
                    .Where(r => r.CreatedAt < cutoffDate)
                    .ToListAsync(stoppingToken);

                if (oldReadings.Any())
                {
                    // 1. Begin explicit database transaction
                    using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);

                    try
                    {
                        // Group by sensor, day, and hour to calculate averages
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

                        // Save aggregated historical data
                        context.SensorReadingsHistory.AddRange(historyRecords);

                        // Remove raw data
                        context.SensorReadings.RemoveRange(oldReadings);

                        await context.SaveChangesAsync(stoppingToken);

                        // 2. Commit transaction if save is successful
                        await transaction.CommitAsync(stoppingToken);

                        _logger.LogInformation("Successfully archived {Count} readings.", oldReadings.Count);
                    }
                    catch (Exception ex)
                    {
                        // 3. Rollback all changes in case of an error (ACID compliance)
                        await transaction.RollbackAsync(stoppingToken);
                        _logger.LogError(ex, "Transaction error during data archiving. Changes rolled back.");
                    }
                }

                // Check the database every 12 hours
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }
    }
}