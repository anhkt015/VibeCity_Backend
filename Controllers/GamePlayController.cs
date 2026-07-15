using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using VibeCity_API.Data;

namespace VibeCity_API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GamePlayController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GamePlayController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. QUẢN LÝ ĐỘNG VẬT (GÀ/GẤU)
        // ==========================================
        [HttpPost("animal/place")]
        public async Task<IActionResult> PlaceAnimal([FromBody] AnimalInstance animal)
        {
            var studentId = User.Identity?.Name;
            animal.OwnerStudentId = studentId!; // Gán chủ sở hữu là người chơi hiện tại
            animal.Id = Guid.NewGuid();

            _context.AnimalInstances.Add(animal);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, animalId = animal.Id });
        }

        [HttpGet("animal/house/{houseOwnerId}")]
        public async Task<IActionResult> GetAnimalsInHouse(string houseOwnerId)
        {
            var animals = await _context.AnimalInstances
                .Where(a => a.HouseOwnerStudentId == houseOwnerId && a.IsActive)
                .ToListAsync();
            return Ok(animals);
        }

        // ==========================================
        // 2. THU HOẠCH GÀ
        // ==========================================
        [HttpPost("chicken/collect")]
        public async Task<IActionResult> CollectChickenIncome([FromBody] string houseOwnerId)
        {
            var income = await _context.HouseChickenIncomes
                .FirstOrDefaultAsync(h => h.HouseOwnerStudentId == houseOwnerId);

            if (income == null || income.PendingCoin <= 0)
                return BadRequest(new { message = "Không có tiền để thu hoạch!" });

            // Logic cộng tiền vào ví người chơi ở đây...
            // Cập nhật last_collected_at và reset pending_coin

            await _context.SaveChangesAsync();
            return Ok(new { success = true, collected = income.PendingCoin });
        }

        // ==========================================
        // 3. ĐỘT KÍCH (RAID)
        // ==========================================
        [HttpPost("raid/start")]
        public async Task<IActionResult> StartRaid([FromBody] string targetStudentId)
        {
            var raid = new RaidSession
            {
                Id = Guid.NewGuid(),
                AttackerStudentId = User.Identity?.Name,
                TargetStudentId = targetStudentId,
                Status = "CREATED",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            _context.RaidSessions.Add(raid);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, raidId = raid.Id });
        }
    }
}