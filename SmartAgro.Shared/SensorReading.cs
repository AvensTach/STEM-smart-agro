using System;
using System.ComponentModel.DataAnnotations;

namespace SmartAgro.Shared
{
    public class SensorReading
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SensorId is mandatory.")]
        public int SensorId { get; set; }

        [Range(-40.0, 80.0, ErrorMessage = "Temperature must be between -40 and 80 °C.")]
        public double Temperature { get; set; }

        [Range(0.0, 100.0, ErrorMessage = "Humidity must be a percentage between 0 and 100.")]
        public double SoilHumidity { get; set; }

        [Range(0.0, 150000.0, ErrorMessage = "Light level out of realistic bounds.")]
        public double LightLevel { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}