using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var threshold = DateTime.UtcNow.AddDays(-30); // Archive data older than 30 days

                    var oldReadings = await context.SensorReadings
                        .Where(r => r.CreatedAt < threshold)
                        .ToListAsync(stoppingToken);

                    if (oldReadings.Any())
                    {
                        // 1. BEGIN EXPLICIT TRANSACTION
                        using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);

                        try
                        {
                            var historyRecords = oldReadings.Select(r => new SensorReadingHistory
                            {
                                Id = r.Id,
                                SensorId = r.SensorId,
                                Temperature = r.Temperature,
                                // Add any other required telemetry properties here
                                CreatedAt = r.CreatedAt
                            });

                            context.SensorReadingsHistory.AddRange(historyRecords);
                            context.SensorReadings.RemoveRange(oldReadings);

                            await context.SaveChangesAsync(stoppingToken);

                            // 2. COMMIT TRANSACTION IF SUCCESSFUL
                            await transaction.CommitAsync(stoppingToken);

                            _logger.LogInformation($"Archived {oldReadings.Count} old readings.");
                        }
                        catch (Exception ex)
                        {
                            // 3. ROLLBACK EVERYTHING ON FAILURE
                            await transaction.RollbackAsync(stoppingToken);
                            _logger.LogError(ex, "Database failure during archive. Transaction safely rolled back.");

                            // Re-throw so our unit test registers the exception!
                            throw;
                        }
                    }
                }
                catch (Exception)
                {
                    // Catch the thrown exception so the background service doesn't permanently crash
                }

                // Wait before running again (e.g., 24 hours)
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}