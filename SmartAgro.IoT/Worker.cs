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
            _httpClient = new HttpClient(); // Our client for sending requests
            _random = new Random();         // Random number generator
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Update this with the real URL when the backend is ready
            string apiUrl = "http://localhost:5297/api/microclimate";

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Generate random microclimate data
                var reading = new SensorReading
                {
                    // Id is omitted; the server's database will generate it
                    Temperature = Math.Round(_random.NextDouble() * (35.0 - 10.0) + 10.0, 1), // From 10.0 to 35.0 °C
                    SoilHumidity = Math.Round(_random.NextDouble() * (80.0 - 30.0) + 30.0, 1), // From 30.0 to 80.0 %
                    LightLevel = _random.Next(100, 1000), // From 100 to 1000 lux
                    CreatedAt = DateTime.UtcNow
                };

                // Output to console to verify the program is running
                _logger.LogInformation("Generated: Temp: {Temp}°C, Humidity: {Hum}%", reading.Temperature, reading.SoilHumidity);

                try
                {
                    // 2. Send data to the server. 
                    // PostAsJsonAsync automatically serializes the 'reading' object to JSON

                    // COMMENTED OUT UNTIL THE SERVER IS READY. 
                    // When your teammate says the API is ready, just uncomment these lines:

                    var response = await _httpClient.PostAsJsonAsync(apiUrl, reading, stoppingToken);
                    _logger.LogInformation("Server response: {StatusCode}", response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to send data: {Message}", ex.Message);
                }

                // 3. Wait for 5 seconds before the next generation
                await Task.Delay(5000, stoppingToken);
            }
        }   
    }
}
