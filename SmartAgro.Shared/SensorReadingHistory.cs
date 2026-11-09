namespace SmartAgro.Shared
{
    public class SensorReadingHistory
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public double Temperature { get; set; }
        public double SoilHumidity { get; set; } 
        public double LightLevel { get; set; }
        public DateTime CreatedAt { get; set; } 
    }
}