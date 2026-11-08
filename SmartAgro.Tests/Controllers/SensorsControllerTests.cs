using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Controllers;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Controllers
{
    public class SensorsControllerTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            // Create a fresh database for each test run
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
                .Options;

            var context = new AppDbContext(options);

            // Seed test data
            context.Sensors.AddRange(
                new SensorConfig { Id = 1, Name = "Test Greenhouse" },
                new SensorConfig { Id = 2, Name = "Test Field" }
            );
            context.SaveChanges();

            return context;
        }

        [Fact]
        public async Task GetSensors_ReturnsAllSeededSensors()
        {
            using var context = GetInMemoryDbContext();
            var controller = new SensorsController(context);

            var result = await controller.GetSensors();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var sensors = Assert.IsAssignableFrom<IEnumerable<SensorConfig>>(okResult.Value);

            Assert.Equal(2, System.Linq.Enumerable.Count(sensors));
        }
    }
}