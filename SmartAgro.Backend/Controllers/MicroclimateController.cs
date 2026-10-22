using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Attributes;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Hubs;
using SmartAgro.Shared;

namespace SmartAgro.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MicroclimateController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<MicroclimateHub> _hubContext;

        public MicroclimateController(AppDbContext context, IHubContext<MicroclimateHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // POST: api/microclimate 
        [HttpPost]
        [ApiKey]
        public async Task<IActionResult> PostReading([FromBody] SensorReading reading)
        {
            reading.CreatedAt = DateTime.UtcNow;

            _context.SensorReadings.Add(reading);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveNewReading", reading);

            return Ok(reading);
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