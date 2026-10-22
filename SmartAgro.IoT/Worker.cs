using System.Net.Http.Json;
using SmartAgro.Shared;

namespace SmartAgro.IoT
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _httpClient;
        private readonly Random _random;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _random = new Random();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Update these URLs to match your actual backend routing
            string configUrl = "http://localhost:5000/api/sensors";
            string telemetryUrl = "http://localhost:5000/api/microclimate";

            List<SensorConfig> activeSensors = new List<SensorConfig>();

            DateTime lastConfigFetch = DateTime.MinValue;
            TimeSpan configRefreshInterval = TimeSpan.FromSeconds(30); // Update frequency (every 30 sec)

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1.Update the configuration if the list is empty OR the update interval has passed
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
                        _logger.LogWarning("No sensors configured or server unreachable. Waiting...");
                        await Task.Delay(5000, stoppingToken);
                        continue; // Skip generation if there are no sensors
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh sensor config. Continuing with previous config.");
                    }
                }

                // 2. Generate and send data for each sensor provided by the server
                foreach (var sensor in activeSensors)
                {
                    var reading = new SensorReading
                    {
                        SensorId = sensor.Id, // Assign the specific ID from the server
                        Temperature = Math.Round(_random.NextDouble() * (35.0 - 10.0) + 10.0, 1),
                        SoilHumidity = Math.Round(_random.NextDouble() * (80.0 - 30.0) + 30.0, 1),
                        LightLevel = _random.Next(100, 1000),
                        CreatedAt = DateTime.UtcNow
                    };

                    _logger.LogInformation("Generated for '{Name}' (ID:{Id}): Temp: {Temp}°C, Humidity: {Hum}%",
                        sensor.Name, sensor.Id, reading.Temperature, reading.SoilHumidity);

                    try
                    {
                        // 3. Send data to the server (Uncomment when API is ready)
                        // var response = await _httpClient.PostAsJsonAsync(telemetryUrl, reading, stoppingToken);
                        // _logger.LogInformation("Server response: {StatusCode}", response.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to send data for sensor {Id}: {Message}", sensor.Id, ex.Message);
                    }
                }

                // 4. Wait for 5 seconds before the next generation cycle
                await Task.Delay(5000, stoppingToken);
            }
        }

        // Helper method to fetch the sensor list from the API
        private async Task<List<SensorConfig>> FetchSensorConfigurationAsync(string url, CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Requesting sensor configuration from {Url}...", url);

                // Expecting the server to return a JSON array like: [{"id": 1, "name": "Greenhouse 1"}, ...]
                var sensors = await _httpClient.GetFromJsonAsync<List<SensorConfig>>(url, stoppingToken);

                if (sensors != null && sensors.Any())
                {
                    _logger.LogInformation("Successfully loaded {Count} sensors.", sensors.Count);
                    return sensors;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to fetch sensor configuration: {Message}", ex.Message);
            }

            return new List<SensorConfig>(); // Return empty list on failure
        }   
    }
}
