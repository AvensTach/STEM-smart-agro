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

        // 1. Wrapper to expose the protected ExecuteAsync method
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
                    CreatedAt = DateTime.UtcNow.AddDays(-3) // Force data to be "old"
                });
                seedContext.SaveChanges();
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped(provider => new AppDbContext(_options));
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var mockLogger = new Mock<ILogger<DataArchiverService>>();
            var service = new TestableDataArchiver(serviceProvider, mockLogger.Object);

            using var cts = new CancellationTokenSource();

            // 2. Trigger the task and cancel it almost immediately so the while loop breaks
            var task = service.RunExecuteAsync(cts.Token);
            cts.CancelAfter(50); // Give it 50ms to run the EF Core logic before cancelling

            try
            {
                await task;
            }
            catch (TaskCanceledException)
            {
                // Expected behavior when breaking the background loop
            }

            // 3. Verify the database transaction worked
            using (var assertContext = new AppDbContext(_options))
            {
                var rawCount = await assertContext.SensorReadings.CountAsync();
                var historyCount = await assertContext.SensorReadingsHistory.CountAsync();

                Assert.Equal(0, rawCount);
                Assert.Equal(1, historyCount);
            }
        }
    }
}