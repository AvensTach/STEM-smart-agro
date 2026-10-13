using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;

namespace SmartAgro.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MicroclimateController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MicroclimateController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/microclimate 
        [HttpPost]
        public async Task<IActionResult> PostReading([FromBody] SensorReading reading)
        {
            reading.CreatedAt = DateTime.UtcNow; 

            _context.SensorReadings.Add(reading);
            await _context.SaveChangesAsync();

            return Ok(reading); // 200 ok
        }

        // GET: api/microclimate
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SensorReading>>> GetHistory()
        {
            // send last 50 readings from the database, ordered by CreatedAt descending
            var history = await _context.SensorReadings
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(history);
        }
    }
}