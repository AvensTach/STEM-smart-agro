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
    }
}