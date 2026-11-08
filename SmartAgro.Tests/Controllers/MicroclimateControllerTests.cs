using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartAgro.Backend.Controllers;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Hubs;
using SmartAgro.Shared;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Controllers
{
    public class MicroclimateControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public MicroclimateControllerTests()
        {
            // Open a connection that persists for the lifetime of the test
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Close();
        }

        [Fact]
        public async Task PostReading_ValidData_SavesToDbAndBroadcasts()
        {
            using var context = new AppDbContext(_options);

            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();
            mockClients.Setup(clients => clients.All).Returns(mockClientProxy.Object);

            var mockHubContext = new Mock<IHubContext<MicroclimateHub>>();
            mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

            var controller = new MicroclimateController(context, mockHubContext.Object);
            var newReading = new SensorReading { SensorId = 1, Temperature = 25.5, SoilHumidity = 40, LightLevel = 1000 };

            var result = await controller.PostReading(newReading);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var savedReading = Assert.IsType<SensorReading>(okResult.Value);

            Assert.Equal(25.5, savedReading.Temperature);

            var dbRecord = await context.SensorReadings.FirstOrDefaultAsync();
            Assert.NotNull(dbRecord);

            // Verify SignalR broadcast was called exactly once
            mockClientProxy.Verify(
                clientProxy => clientProxy.SendCoreAsync(
                    "ReceiveNewReading",
                    It.Is<object[]>(o => o != null && o.Length == 1),
                    default),
                Times.Once);
        }
        [Fact]
        public async Task PostReading_InvalidModelState_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_options);
            var mockHubContext = new Mock<IHubContext<MicroclimateHub>>();
            var controller = new MicroclimateController(context, mockHubContext.Object);

            // Simulate validation failure (e.g., missing [Required] or out of [Range])
            controller.ModelState.AddModelError("Temperature", "Out of range");

            var result = await controller.PostReading(new SensorReading());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetHistory_ReturnsExpectedData()
        {
            using var context = new AppDbContext(_options);
            // Add dummy data for the GET request
            context.SensorReadings.Add(new SensorReading { SensorId = 1, Temperature = 20 });
            await context.SaveChangesAsync();

            var mockHubContext = new Mock<IHubContext<MicroclimateHub>>();
            var controller = new MicroclimateController(context, mockHubContext.Object);

            // Adapt "GetHistory" to match the actual name of your GET method in the controller
            var result = await controller.GetHistory();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var data = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<SensorReading>>(okResult.Value);
            Assert.NotEmpty(data);
        }
    }
}