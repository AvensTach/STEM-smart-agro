using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;
using SmartAgro.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartAgro.Backend.Controllers
{
    [Authorize] // Requires a valid JWT token
    [Route("api/[controller]")]
    [ApiController]
    public class SensorsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SensorsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(userIdClaim);
        }

        // GET: api/sensors
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SensorConfig>>> GetSensors()
        {
            var userId = GetCurrentUserId();

            // Send list of ONLY the logged-in user's sensors
            var sensors = await _context.Sensors
                .Where(s => s.UserId == userId)
                .ToListAsync();

            return Ok(sensors);
        }

        // POST: api/sensors
        [HttpPost]
        public async Task<ActionResult<SensorConfig>> AddSensor([FromBody] SensorConfig sensor)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Security check: Force the new sensor to belong to the authenticated user.
            // This prevents a malicious user from passing someone else's UserId in the JSON body.
            sensor.UserId = GetCurrentUserId();

            _context.Sensors.Add(sensor);
            await _context.SaveChangesAsync();

            // Return 201 Created with the newly generated sensor data (including its new database ID)
            return CreatedAtAction(nameof(GetSensors), new { id = sensor.Id }, sensor);
        }
    }
}