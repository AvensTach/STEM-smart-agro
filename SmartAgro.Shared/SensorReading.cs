using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAgro.Shared
{
    public class SensorReading
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public double Temperature { get; set; }
        public double SoilHumidity { get; set; } 
        public double LightLevel { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
