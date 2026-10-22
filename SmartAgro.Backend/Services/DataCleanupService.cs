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

                    var deletedCount = await context.SensorReadings
                        .Where(r => r.CreatedAt < cutoffDate)
                        .ExecuteDeleteAsync(stoppingToken);

                    _logger.LogInformation("Очищення бази: видалено {Count} старих записів.", deletedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Помилка під час очищення бази даних: {Message}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}