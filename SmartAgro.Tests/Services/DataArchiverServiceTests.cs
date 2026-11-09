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
    public class DataArchiverServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public DataArchiverServiceTests()
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

        private class TestableDataArchiver : DataArchiverService
        {
            public TestableDataArchiver(IServiceProvider sp, ILogger<DataArchiverService> logger)
                : base(sp, logger) { }

            public Task RunExecuteAsync(CancellationToken token)
            {
                return base.ExecuteAsync(token);
            }
        }

        [Fact]
        public async Task ExecuteAsync_ArchivesOldData_AndRemovesRawData()
        {
            using (var seedContext = new AppDbContext(_options))
            {
                seedContext.Database.EnsureCreated();
                seedContext.SensorReadings.Add(new SensorReading
                {
                    SensorId = 1,
                    Temperature = 20,
                    // CHANGE THIS to be older than 30 days so the archiver picks it up
                    CreatedAt = DateTime.UtcNow.AddDays(-40)
                });
                seedContext.SaveChanges();
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped(provider => new AppDbContext(_options));
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var mockLogger = new Mock<ILogger<DataArchiverService>>();
            var service = new TestableDataArchiver(serviceProvider, mockLogger.Object);

            using var cts = new CancellationTokenSource();
            var task = service.RunExecuteAsync(cts.Token);
            cts.CancelAfter(50);

            try { await task; } catch (TaskCanceledException) { }

            using (var assertContext = new AppDbContext(_options))
            {
                var rawCount = await assertContext.SensorReadings.CountAsync();
                var historyCount = await assertContext.SensorReadingsHistory.CountAsync();

                Assert.Equal(0, rawCount);
                Assert.Equal(1, historyCount);
            }
        }

        [Fact]
        public async Task ExecuteAsync_WhenExceptionOccurs_RollsBackTransaction()
        {
            // Arrange: Seed a primary key conflict to force a crash during the transaction
            using (var seedContext = new AppDbContext(_options))
            {
                seedContext.Database.EnsureCreated();

                // Active reading to be archived
                seedContext.SensorReadings.Add(new SensorReading
                {
                    Id = 500,
                    SensorId = 1,
                    Temperature = 20,
                    CreatedAt = DateTime.UtcNow.AddDays(-40)
                });

                // Conflicting history reading (will cause a crash when ID 500 is archived)
                seedContext.SensorReadingsHistory.Add(new SensorReadingHistory
                {
                    Id = 500,
                    SensorId = 1,
                    Temperature = 99,
                    CreatedAt = DateTime.UtcNow.AddDays(-40)
                });

                seedContext.SaveChanges();
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped(provider => new AppDbContext(_options));
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var mockLogger = new Mock<ILogger<DataArchiverService>>();
            var service = new TestableDataArchiver(serviceProvider, mockLogger.Object);

            using var cts = new CancellationTokenSource();
            var task = service.RunExecuteAsync(cts.Token);
            cts.CancelAfter(200);

            try { await task; } catch (Exception) { }

            // Assert: Verify the active reading was NOT deleted because the transaction rolled back
            using (var assertContext = new AppDbContext(_options))
            {
                var rawReadingExists = await assertContext.SensorReadings.AnyAsync(r => r.Id == 500);
                Assert.True(rawReadingExists, "Transaction failed to roll back! Raw reading was deleted.");
            }
        }
    }
}