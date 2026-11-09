using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartAgro.Shared
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Username { get; set; }

        [Required]
        public required string PasswordHash { get; set; }

        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<SensorConfig> Sensors { get; set; } = new List<SensorConfig>();
    }
}