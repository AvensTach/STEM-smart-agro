using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartAgro.Shared
{
    public class SensorConfig
    {
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public int UserId { get; set; }

    
    }
}