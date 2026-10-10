using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAgro.Shared
{
    public class SensorReading
    {
        public int Id { get; set; } // Унікальний ідентифікатор у базі
        public double Temperature { get; set; } // Температура
        public double SoilHumidity { get; set; } // Вологість ґрунту
        public double LightLevel { get; set; } // Рівень освітлення
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Час запису
    }
}
