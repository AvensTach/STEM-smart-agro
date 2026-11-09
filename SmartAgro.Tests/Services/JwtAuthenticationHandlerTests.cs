using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SmartAgro.IoT;
using Xunit;

namespace SmartAgro.Tests.Services
{
    public class JwtAuthenticationHandlerTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<JwtAuthenticationHandler>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _innerHandlerMock;

        public JwtAuthenticationHandlerTests()
        {
            _loggerMock = new Mock<ILogger<JwtAuthenticationHandler>>();
            _configMock = new Mock<IConfiguration>();
            _innerHandlerMock = new Mock<HttpMessageHandler>();

            // Мокуємо конфігурацію IotSettings
            _configMock.Setup(c => c["IotSettings:BaseUrl"]).Returns("http://localhost:5297");
            _configMock.Setup(c => c["IotSettings:Username"]).Returns("test-worker");
            _configMock.Setup(c => c["IotSettings:Password"]).Returns("Password123!");
        }

        [Fact]
        public async Task SendAsync_AttachesBearerToken_WhenTokenIsSet()
        {
            // Arrange: set the token via reflection to test header injection
            var handler = new JwtAuthenticationHandler(_configMock.Object, _loggerMock.Object)
            {
                InnerHandler = _innerHandlerMock.Object
            };

            SetPrivateField(handler, "_currentToken", "fake-jwt-token");

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5297/api/sensors");

            // Act
            var response = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            Assert.Equal("fake-jwt-token", request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task SendAsync_RetriesRequestWithNewToken_WhenResponseIs401Unauthorized()
        {
            // Arrange
            var handler = new JwtAuthenticationHandler(_configMock.Object, _loggerMock.Object)
            {
                InnerHandler = _innerHandlerMock.Object
            };

            SetPrivateField(handler, "_currentToken", "old-expired-token");

            int callCount = 0;

            _innerHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5297/api/sensors");

            // Act
            var response = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            _innerHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task CloneHttpRequestMessageAsync_ClonesRequestMethodUriAndHeadersCorrectly()
        {
            // Arrange: checking the helper method for cloning requests
            
            var originalRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5297/api/microclimate")
            {
                Content = new StringContent("{\"temp\":22.5}", System.Text.Encoding.UTF8, "application/json")
            };
            originalRequest.Headers.Add("Custom-Header", "TestValue");

            // Act
            var cloneMethod = typeof(JwtAuthenticationHandler)
                .GetMethod("CloneHttpRequestMessageAsync", BindingFlags.NonPublic | BindingFlags.Static);

            var task = (Task<HttpRequestMessage>)cloneMethod!.Invoke(null, new object[] { originalRequest })!;
            var clonedRequest = await task;

            // Assert
            Assert.NotNull(clonedRequest);
            Assert.Equal(originalRequest.Method, clonedRequest.Method);
            Assert.Equal(originalRequest.RequestUri, clonedRequest.RequestUri);
            Assert.True(clonedRequest.Headers.Contains("Custom-Header"));

            var clonedContent = await clonedRequest.Content!.ReadAsStringAsync();
            Assert.Equal("{\"temp\":22.5}", clonedContent);
        }

        #region Helper Methods

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        #endregion
    }
}
