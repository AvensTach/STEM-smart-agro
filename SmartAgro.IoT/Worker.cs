using Microsoft.Extensions.Configuration;
using Serilog;
using SmartAgro.Shared;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Http;
using System.Net.Http.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SmartAgro.IoT
{
    public class Worker : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<Worker> _logger;
        private readonly Random _random;

        // Constants for Boundary Checks
        private const double MinTemperature = -40.0;
        private const double MaxTemperature = 80.0;
        private const double MinHumidity = 0.0;
        private const double MaxHumidity = 100.0;
        private const int MinLightLevel = 0;

        // Updating the constructor: accepting IHttpClientFactory instead of IConfiguration
        public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _random = new Random();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // obtain a named client "IotClient" (it automatically adds a Bearer Token and handles 401s)
            var httpClient = _httpClientFactory.CreateClient("IotClient");

            // Relative paths (since BaseAddress="http://localhost:5297" is already specified in Program.cs)
            string configUrl = "/api/sensors";
            string telemetryUrl = "/api/microclimate";

            // L45: Simplified collection initialization
            List<SensorConfig> activeSensors = [];

            DateTime lastConfigFetch = DateTime.MinValue;
            TimeSpan configRefreshInterval = TimeSpan.FromSeconds(60);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Sensor configuration update
                if (activeSensors.Count == 0 || DateTime.UtcNow - lastConfigFetch > configRefreshInterval)
                {
                    var refreshedSensors = await FetchSensorConfigurationAsync(httpClient, configUrl, stoppingToken);

                    if (refreshedSensors.Count > 0)
                    {
                        activeSensors = refreshedSensors;
                        lastConfigFetch = DateTime.UtcNow;
                    }
                    else if (activeSensors.Count == 0)
                    {
                        _logger.LogWarning("No sensors configured or server unreachable. Retrying in 5 seconds...");
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh sensor config. Continuing with previous config.");
                    }
                }

                // Generation and transmission of data for each sensor
                foreach (var sensor in activeSensors)
                {
                    var reading = GenerateReading(sensor.Id);

                    // Boundary Checks
                    if (!ValidateSensorReading(reading, sensor.Name))
                    {
                        continue; // Skipping invalid data
                    }

                    // L85: Avoid evaluating sensor.Name inside interpolated string
                    _logger.LogInformation("Generated for '{Name}' (ID:{Id}): Temp: {Temp}°C, Humidity: {Hum}%, Light: {Light}",
                        sensor.Name, sensor.Id, reading.Temperature, reading.SoilHumidity, reading.LightLevel);

                    // Network Fault Tolerance
                    await SendTelemetryAsync(httpClient, telemetryUrl, reading, sensor.Id, stoppingToken);
                }

                // Wait for 60 seconds before the next generation cycle
                await Task.Delay(60000, stoppingToken);
            }
        }

        private SensorReading GenerateReading(int sensorId)
        {
            return new SensorReading
            {
                SensorId = sensorId,
                Temperature = Math.Round(_random.NextDouble() * (35.0 - 10.0) + 10.0, 1),
                SoilHumidity = Math.Round(_random.NextDouble() * (80.0 - 30.0) + 30.0, 1),
                LightLevel = _random.Next(100, 1000),
                CreatedAt = DateTime.UtcNow
            };
        }

        internal bool ValidateSensorReading(SensorReading reading, string sensorName)
        {
            if (reading.Temperature < MinTemperature || reading.Temperature > MaxTemperature)
            {
                _logger.LogWarning("Boundary check failed for '{Name}' (ID:{Id}): Temperature {Temp}°C out of range [{Min}, {Max}]",
                    sensorName, reading.SensorId, reading.Temperature, MinTemperature, MaxTemperature);
                return false;
            }

            if (reading.SoilHumidity < MinHumidity || reading.SoilHumidity > MaxHumidity)
            {
                _logger.LogWarning("Boundary check failed for '{Name}' (ID:{Id}): Humidity {Hum}% out of range [{Min}, {Max}]",
                    sensorName, reading.SensorId, reading.SoilHumidity, MinHumidity, MaxHumidity);
                return false;
            }

            if (reading.LightLevel < MinLightLevel)
            {
                _logger.LogWarning("Boundary check failed for '{Name}' (ID:{Id}): Light level {Light} is negative",
                    sensorName, reading.SensorId, reading.LightLevel);
                return false;
            }

            return true;
        }

        internal async Task SendTelemetryAsync(HttpClient httpClient, string url, SensorReading reading, int sensorId, CancellationToken stoppingToken)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync(url, reading, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent data for sensor {Id}. Status: {StatusCode}", sensorId, response.StatusCode);
                }
                else
                {
                    // L143: Explicitly convert status code to int instead of passing enum directly
                    int statusCodeValue = (int)response.StatusCode;
                    _logger.LogWarning("Server returned non-success code for sensor {Id}: {StatusCode} ({Reason})",
                        sensorId, statusCodeValue, response.ReasonPhrase);
                }
            }
            catch (HttpRequestException ex)
            {
                // L157: Pass ex.Message explicitly
                string errorMessage = ex.Message;
                _logger.LogWarning("Backend offline or unreachable for sensor {Id}: {Message}", sensorId, errorMessage);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Telemetry request cancelled for sensor {Id} due to service shutdown.", sensorId);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Telemetry request timed out for sensor {Id}.", sensorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending telemetry for sensor {Id}", sensorId);
            }
        }

        internal async Task<List<SensorConfig>> FetchSensorConfigurationAsync(HttpClient httpClient, string url, CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Requesting sensor configuration from {Url}...", url);

                var sensors = await httpClient.GetFromJsonAsync<List<SensorConfig>>(url, stoppingToken);

                // L177: Replaced Any() with Count > 0 for List<T>
                if (sensors != null && sensors.Count > 0)
                {
                    // L179: Access sensors.Count via local variable or direct format
                    int count = sensors.Count;
                    _logger.LogInformation("Successfully loaded {Count} sensors.", count);
                    return sensors;
                }
            }
            catch (HttpRequestException ex)
            {
                string errorMessage = ex.Message;
                _logger.LogWarning("Failed to fetch sensor configuration (Backend offline): {Message}", errorMessage);
            }
            catch (TaskCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout fetching sensor configuration from {Url}", url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching sensor configuration.");
            }

            // L196: Simplified collection initialization
            return [];
        }
    }
}