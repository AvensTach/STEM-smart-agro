using Microsoft.Extensions.Configuration;
using Serilog;
using SmartAgro.Shared;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SmartAgro.IoT
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _httpClient;
        private readonly Random _random;
        private readonly IConfiguration _configuration;

        // Constants for Boundary Checks
        private const double MinTemperature = -40.0;
        private const double MaxTemperature = 80.0;
        private const double MinHumidity = 0.0;
        private const double MaxHumidity = 100.0;
        private const int MinLightLevel = 0;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = new HttpClient();
            _random = new Random();

            var apiKey = _configuration["ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
            else
            {
                _logger.LogWarning("API Key is missing in User Secrets!");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Update these URLs to match your actual backend routing
            string configUrl = "http://localhost:5297/api/sensors";
            string telemetryUrl = "http://localhost:5297/api/microclimate";

            List<SensorConfig> activeSensors = new List<SensorConfig>();

            DateTime lastConfigFetch = DateTime.MinValue;
            TimeSpan configRefreshInterval = TimeSpan.FromSeconds(60); // Update frequency (every 60 sec)

            while (!stoppingToken.IsCancellationRequested)
            {
                // Sensor configuration update
                if (activeSensors.Count == 0 || DateTime.UtcNow - lastConfigFetch > configRefreshInterval)
                {
                    var refreshedSensors = await FetchSensorConfigurationAsync(configUrl, stoppingToken);

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
                    var reading = new SensorReading
                    {
                        SensorId = sensor.Id,
                        Temperature = Math.Round(_random.NextDouble() * (35.0 - 10.0) + 10.0, 1),
                        SoilHumidity = Math.Round(_random.NextDouble() * (80.0 - 30.0) + 30.0, 1),
                        LightLevel = _random.Next(100, 1000),
                        CreatedAt = DateTime.UtcNow
                    };

                    // call our new sending method with a retry policy
                    await SendReadingWithRetryAsync(telemetryUrl, reading, stoppingToken);
                }

                // 4. Wait for 60 seconds before the next generation cycle
                await Task.Delay(60000, stoppingToken);
            }
        }

        /// <summary>
        /// Requesting sensor configuration with graceful handling of network errors.
        /// </summary>
        private async Task<List<SensorConfig>> FetchSensorConfigurationAsync(string url, CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Requesting sensor configuration from {Url}...", url);

                var sensors = await _httpClient.GetFromJsonAsync<List<SensorConfig>>(url, stoppingToken);

                if (sensors != null && sensors.Any())
                {
                    _logger.LogInformation("Successfully loaded {Count} sensors.", sensors.Count);
                    return sensors;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Failed to fetch sensor configuration (Backend offline): {Message}", ex.Message);
            }
            catch (TaskCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout fetching sensor configuration from {Url}", url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching sensor configuration.");
            }

            return new List<SensorConfig>();
        }
        private async Task SendReadingWithRetryAsync(string url, SensorReading reading, CancellationToken stoppingToken)
        {
            int maxRetries = 3;
            int delayMs = 1000;

            for (int retry = 1; retry <= maxRetries; retry++)
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync(url, reading, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Sensor {SensorId} - Data sent successfully.", reading.SensorId);
                        return;
                    }

                    _logger.LogWarning("Sensor {SensorId} - Attempt {Retry} failed with status: {Status}",
                        reading.SensorId, retry, response.StatusCode);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError("Sensor {SensorId} - Network error on attempt {Retry}: {Message}",
                        reading.SensorId, retry, ex.Message);
                }

                if (retry < maxRetries)
                {
                    await Task.Delay(delayMs * retry, stoppingToken); // Exponential pause (1s, 2s, 3s)
                }
            }

            _logger.LogError("Sensor {SensorId} - Failed to send telemetry after {Max} attempts.", reading.SensorId, maxRetries);
        }
    }
}