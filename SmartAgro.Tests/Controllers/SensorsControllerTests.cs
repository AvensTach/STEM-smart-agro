using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Controllers;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Controllers
{
    public class SensorsControllerTests
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

        // Helper to inject a mock User into the controller
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
        public async Task GetSensors_ReturnsOnlyUserSensors()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();

            // Use IDs that don't conflict with AppDbContext seed data (Id 1 & 2)
            dbContext.Users.Add(new User { Id = 101, Username = "user1", PasswordHash = "hash1" });
            dbContext.Users.Add(new User { Id = 102, Username = "user2", PasswordHash = "hash2" });

            dbContext.Sensors.Add(new SensorConfig { Id = 101, UserId = 101, Name = "User 1 Sensor" });
            dbContext.Sensors.Add(new SensorConfig { Id = 102, UserId = 102, Name = "User 2 Sensor" });
            await dbContext.SaveChangesAsync();

            var controller = new SensorsController(dbContext);

            // Mock authentication as User 101
            MockUserContext(controller, 101);

            // Act
            var result = await controller.GetSensors();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var sensors = Assert.IsAssignableFrom<IEnumerable<SensorConfig>>(okResult.Value);

            Assert.Single(sensors);
            Assert.Equal("User 1 Sensor", sensors.First().Name);
        }
        [Fact]
        public async Task AddSensor_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();
            var controller = new SensorsController(dbContext);

            MockUserContext(controller, 101);

            // Simulate missing "Name" property in JSON
            controller.ModelState.AddModelError("Name", "Name is required.");

            var invalidSensor = new SensorConfig { UserId = 101, Name = null! };

            // Act
            var result = await controller.AddSensor(invalidSensor);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}