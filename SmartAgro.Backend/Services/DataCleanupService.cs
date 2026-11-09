using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;

namespace SmartAgro.Backend.Services
{
    public class DataCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataCleanupService> _logger;

        public DataCleanupService(IServiceProvider serviceProvider, ILogger<DataCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var cutoffDate = DateTime.UtcNow.AddDays(-7);

                    // 1. Fetch records first to avoid SQLite SQL translation errors with dates
                    var oldRecords = await context.SensorReadings
                        .Where(r => r.CreatedAt < cutoffDate)
                        .ToListAsync(stoppingToken);

                    // 2. Remove using standard change tracking
                    if (oldRecords.Any())
                    {
                        context.SensorReadings.RemoveRange(oldRecords);
                        await context.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Database cleanup: removed {Count} old records.", oldRecords.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error during database cleanup: {Message}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}