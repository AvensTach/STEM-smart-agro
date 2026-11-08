using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Services;
using SmartAgro.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Services
{
    public class DataCleanupServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public DataCleanupServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
        }

        public void Dispose()
        {
            _connection.Close();
        }

        private class TestableDataCleanupService : DataCleanupService
        {
            public TestableDataCleanupService(IServiceProvider sp, ILogger<DataCleanupService> logger)
                : base(sp, logger) { }

            public Task RunExecuteAsync(CancellationToken token)
            {
                return base.ExecuteAsync(token);
            }
        }

        [Fact]
        public async Task ExecuteAsync_RemovesOldDataSuccessfully()
        {
            // 1. Seed the raw SensorReadings table
            using (var seedContext = new AppDbContext(_options))
            {
                seedContext.Database.EnsureCreated();
                seedContext.SensorReadings.AddRange(
                    new SensorReading { SensorId = 1, Temperature = 20, CreatedAt = DateTime.UtcNow.AddDays(-10) }, // Will be deleted
                    new SensorReading { SensorId = 1, Temperature = 22, CreatedAt = DateTime.UtcNow.AddDays(-2) }   // Will be kept
                );
                seedContext.SaveChanges();
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped(provider => new AppDbContext(_options));
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var mockLogger = new Mock<ILogger<DataCleanupService>>();
            var service = new TestableDataCleanupService(serviceProvider, mockLogger.Object);

            using var cts = new CancellationTokenSource();

            // 2. Start the service
            var task = service.RunExecuteAsync(cts.Token);

            // 3. Polling Strategy: Actively wait until the database changes (max 3 seconds)
            int remainingCount = 2;
            using (var assertContext = new AppDbContext(_options))
            {
                for (int i = 0; i < 30; i++)
                {
                    remainingCount = await assertContext.SensorReadings.CountAsync();

                    if (remainingCount == 1)
                    {
                        break; // EF Core finished! Exit the checking loop.
                    }

                    await Task.Delay(100); // Check again in 100ms
                }
            }

            // 4. Safely stop the service now that the work is verified
            cts.Cancel();
            try { await task; } catch (TaskCanceledException) { }

            // 5. Final assertion
            Assert.Equal(1, remainingCount);
        }
    }
}