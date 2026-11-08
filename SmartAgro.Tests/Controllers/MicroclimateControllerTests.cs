using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartAgro.Backend.Controllers;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Hubs;
using SmartAgro.Shared;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Controllers
{
    public class MicroclimateControllerTests
    {
        private async Task<AppDbContext> GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
                .Options;

            var databaseContext = new AppDbContext(options);
            await databaseContext.Database.EnsureCreatedAsync();
            return databaseContext;
        }

        private void MockUserContext(ControllerBase controller, int userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        [Fact]
        public async Task PostReading_ValidDataAndOwner_BroadcastsAndSaves()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();

            // Seed a sensor owned by User 101
            dbContext.Sensors.Add(new SensorConfig { Id = 101, UserId = 101, Name = "Test Sensor" });
            await dbContext.SaveChangesAsync();

            var mockHub = new Mock<IHubContext<MicroclimateHub>>();
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();

            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            var controller = new MicroclimateController(dbContext, mockHub.Object);
            MockUserContext(controller, 101); // Mocking as User 101

            var newReading = new SensorReading { SensorId = 101, Temperature = 22.5, SoilHumidity = 50.0 };

            // Act
            var result = await controller.PostReading(newReading);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            mockClientProxy.Verify(
                c => c.SendCoreAsync("ReceiveNewReading", It.IsAny<object[]>(), default),
                Times.Once);
        }

        [Fact]
        public async Task PostReading_NotOwner_ReturnsForbid()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();

            // Seed a sensor owned by User 102
            dbContext.Sensors.Add(new SensorConfig { Id = 102, UserId = 102, Name = "Other User Sensor" });
            await dbContext.SaveChangesAsync();

            var mockHub = new Mock<IHubContext<MicroclimateHub>>();
            var controller = new MicroclimateController(dbContext, mockHub.Object);

            MockUserContext(controller, 101); // User 101 trying to post to User 102's sensor

            var newReading = new SensorReading { SensorId = 102, Temperature = 22.5, SoilHumidity = 50.0 };

            // Act
            var result = await controller.PostReading(newReading);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
        [Fact]
        public async Task PostReading_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();
            var mockHub = new Mock<IHubContext<MicroclimateHub>>();
            var controller = new MicroclimateController(dbContext, mockHub.Object);

            MockUserContext(controller, 101);

            // Force a validation error (simulating a bad JSON payload from the frontend/IoT device)
            controller.ModelState.AddModelError("Temperature", "Temperature is out of bounds.");

            var invalidReading = new SensorReading { SensorId = 101, Temperature = 9999 };

            // Act
            var result = await controller.PostReading(invalidReading);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}