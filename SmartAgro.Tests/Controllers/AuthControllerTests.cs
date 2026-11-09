using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartAgro.Backend.Controllers;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Controllers
{
    public class AuthControllerTests
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

        private IConfiguration GetMockConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string> {
                {"Jwt:Key", "SuperSecretKeyForUnitTestingMustBe32Chars!"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [Fact]
        public async Task Register_NewUser_ReturnsOk()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();
            var config = GetMockConfiguration();
            var controller = new AuthController(dbContext, config);

            var request = new AuthController.UserDto { Username = "newuser", Password = "password123" };

            // Act
            var result = await controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.True(await dbContext.Users.AnyAsync(u => u.Username == "newuser"));
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var dbContext = await GetDatabaseContext();
            var config = GetMockConfiguration();
            var controller = new AuthController(dbContext, config);

            var password = "password123";
            dbContext.Users.Add(new User
            {
                Username = "testuser",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await dbContext.SaveChangesAsync();

            var request = new AuthController.UserDto { Username = "testuser", Password = password };

            // Act
            var result = await controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var value = okResult.Value;

            // Use reflection to check if the anonymous object has a "token" property
            var tokenProperty = value.GetType().GetProperty("token");
            Assert.NotNull(tokenProperty);
        }
    }
}