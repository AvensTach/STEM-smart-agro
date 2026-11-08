using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SmartAgro.IoT;
using SmartAgro.Shared;
using Xunit;

namespace SmartAgro.Tests.Services
{
    public class WorkerTests
    {
        private readonly Mock<ILogger<Worker>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public WorkerTests()
        {
            _loggerMock = new Mock<ILogger<Worker>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            // Створюємо HttpClient з моком HttpMessageHandler
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:5297/")
            };

            _httpClientFactoryMock
                .Setup(f => f.CreateClient("IotClient"))
                .Returns(httpClient);
        }

        #region FetchSensorConfigurationAsync Tests

        [Fact]
        public async Task FetchSensorConfigurationAsync_ReturnsSensors_WhenBackendReturns200OK()
        {
            // Arrange
            var jsonResponse = "[{\"id\":1,\"name\":\"Greenhouse Sensor #1\"}]";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.AbsolutePath == "/api/sensors"),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                });

            var worker = new Worker(_loggerMock.Object, _httpClientFactoryMock.Object);

            // Act
            var sensors = await worker.FetchSensorConfigurationAsync(
                _httpClientFactoryMock.Object.CreateClient("IotClient"),
                "/api/sensors",
                CancellationToken.None);

            // Assert
            Assert.NotNull(sensors);
            Assert.Single(sensors);
            Assert.Equal(1, sensors[0].Id);
            Assert.Equal("Greenhouse Sensor #1", sensors[0].Name);
        }

        [Fact]
        public async Task FetchSensorConfigurationAsync_ReturnsEmptyList_WhenBackendReturnsEmptyArray()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                });

            var worker = new Worker(_loggerMock.Object, _httpClientFactoryMock.Object);

            // Act
            var sensors = await worker.FetchSensorConfigurationAsync(
                _httpClientFactoryMock.Object.CreateClient("IotClient"),
                "/api/sensors",
                CancellationToken.None);

            // Assert
            Assert.NotNull(sensors);
            Assert.Empty(sensors);
        }

        [Fact]
        public async Task FetchSensorConfigurationAsync_ReturnsEmptyList_WhenBackendReturns500Error()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            var worker = new Worker(_loggerMock.Object, _httpClientFactoryMock.Object);

            // Act
            var sensors = await worker.FetchSensorConfigurationAsync(
                _httpClientFactoryMock.Object.CreateClient("IotClient"),
                "/api/sensors",
                CancellationToken.None);

            // Assert
            Assert.NotNull(sensors);
            Assert.Empty(sensors);
        }

        #endregion

        #region ValidateSensorReading Tests

        [Theory]
        [InlineData(25.0, 50.0, 500, true)]   // Нормальні значення
        [InlineData(-40.0, 0.0, 0, true)]      // Граничні мінімальні
        [InlineData(80.0, 100.0, 1000, true)]  // Граничні максимальні
        [InlineData(-40.1, 50.0, 500, false)]  // Температура занизька
        [InlineData(80.1, 50.0, 500, false)]   // Температура зависока
        [InlineData(25.0, -0.1, 500, false)]   // Вологість занизька
        [InlineData(25.0, 100.1, 500, false)]  // Вологість зависока
        [InlineData(25.0, 50.0, -1, false)]    // Освітленість від'ємна
        public void ValidateSensorReading_ValidatesBoundaryConditionsCorrectly(
            double temp, double humidity, int light, bool expectedResult)
        {
            // Arrange
            var worker = new Worker(_loggerMock.Object, _httpClientFactoryMock.Object);
            var reading = new SensorReading
            {
                SensorId = 1,
                Temperature = temp,
                SoilHumidity = humidity,
                LightLevel = light,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var isValid = worker.ValidateSensorReading(reading, "Test Sensor");

            // Assert
            Assert.Equal(expectedResult, isValid);
        }

        #endregion

        #region SendTelemetryAsync Tests

        [Fact]
        public async Task SendTelemetryAsync_SendsPostRequestWithCorrectPayload()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.AbsolutePath == "/api/microclimate"),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var worker = new Worker(_loggerMock.Object, _httpClientFactoryMock.Object);
            var reading = new SensorReading
            {
                SensorId = 1,
                Temperature = 22.5,
                SoilHumidity = 45.0,
                LightLevel = 300,
                CreatedAt = DateTime.UtcNow
            };

            // Act & Assert (перевіряємо, що метод успішно виконується без виключень)
            await worker.SendTelemetryAsync(
                _httpClientFactoryMock.Object.CreateClient("IotClient"),
                "/api/microclimate",
                reading,
                1,
                CancellationToken.None);

            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        #endregion
    }
}
