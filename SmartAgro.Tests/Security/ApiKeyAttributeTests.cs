using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartAgro.Backend.Attributes;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SmartAgro.Tests.Security
{
    public class ApiKeyAttributeTests
    {
        private const string ValidApiKey = "TestSecretKey123!";

        private ActionExecutingContext CreateMockContext(string? providedApiKey)
        {
            // Allow nullable values in the dictionary to satisfy IConfigurationBuilder
            var inMemorySettings = new Dictionary<string, string?> { { "ApiKey", ValidApiKey } };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddSingleton(configuration)
                .BuildServiceProvider();

            var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

            if (providedApiKey != null)
            {
                httpContext.Request.Headers["X-Api-Key"] = providedApiKey;
            }

            var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());

            // Pass Dictionary<string, object?> and new object() to satisfy non-nullable constraints
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        }

        [Fact]
        public async Task OnActionExecutionAsync_MissingHeader_ReturnsUnauthorized()
        {
            var filter = new ApiKeyAttribute();
            var context = CreateMockContext(null);

            // Pass new object() instead of null for the controller parameter
            await filter.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object())));

            var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
            Assert.Equal("API Key is missing", result.Value);
        }

        [Fact]
        public async Task OnActionExecutionAsync_InvalidKey_ReturnsUnauthorized()
        {
            var filter = new ApiKeyAttribute();
            var context = CreateMockContext("WrongKey");

            await filter.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object())));

            var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
            Assert.Equal("Invalid API Key", result.Value);
        }

        [Fact]
        public async Task OnActionExecutionAsync_ValidKey_ContinuesExecution()
        {
            var filter = new ApiKeyAttribute();
            var context = CreateMockContext(ValidApiKey);
            bool nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
            });

            Assert.Null(context.Result);
            Assert.True(nextCalled);
        }
    }
}