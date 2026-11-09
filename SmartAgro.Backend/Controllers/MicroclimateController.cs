using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Hubs;
using SmartAgro.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartAgro.Backend.Controllers
{
    [Authorize] // Replaces [ApiKey]
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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(userIdClaim);
        }

        // POST: api/microclimate 
        [HttpPost]
        public async Task<IActionResult> PostReading([FromBody] SensorReading reading)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();

            // Security Check: Verify the user owns the sensor before saving telemetry
            var ownsSensor = await _context.Sensors
                .AnyAsync(s => s.Id == reading.SensorId && s.UserId == userId);

            // NEW: Block access ONLY if they don't own the sensor AND they aren't an Admin
            if (!ownsSensor && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            reading.CreatedAt = DateTime.UtcNow;
            _context.SensorReadings.Add(reading);
            await _context.SaveChangesAsync();

            // Broadcast the new reading via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveNewReading", reading);

            return Ok(reading);
        }

        // GET: api/microclimate
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SensorReading>>> GetHistory()
        {
            if (User.IsInRole("Admin"))
            {
                var adminHistory = await _context.SensorReadings
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                return Ok(adminHistory);
            }

            var userId = GetCurrentUserId();

            // 1. Get IDs of sensors owned by the user
            var userSensorIds = await _context.Sensors
                .Where(s => s.UserId == userId)
                .Select(s => s.Id)
                .ToListAsync();

            // 2. Filter readings to include only those from the user's sensors
            var history = await _context.SensorReadings
                .Where(r => userSensorIds.Contains(r.SensorId))
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(history);
        }
    }
}