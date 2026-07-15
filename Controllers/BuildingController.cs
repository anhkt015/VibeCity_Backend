using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using VibeCity_API.Data;
using VibeCity_API.Services;

namespace VibeCity_API.Data
{
    [Route("api/[controller]")]
    [ApiController]
    public class BuildingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IChickenIncomeService _incomeService;

        public BuildingController(
            AppDbContext context,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            IChickenIncomeService incomeService)
        {
            _context = context;
            _configuration = configuration;
            _jwtTokenService = jwtTokenService;
            _incomeService = incomeService;
        }

        // ==========================================
        // PHASE 1: HỆ THỐNG XÂY DỰNG & AUTH GỐC
        // ==========================================

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BuildingDto data)
        {
            if (data == null) return BadRequest(new { error = "Dữ liệu trống!" });
            var currentStudentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentStudentId)) return Unauthorized(new { error = "Chưa xác thực!" });

            data.StudentId = currentStudentId;
            try
            {
                data.Id = 0;
                data.Timestamp = DateTime.UtcNow;
                _context.Buildings.Add(data);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Lưu thành công!", id = data.Id, status = "Success" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi lưu nhà!", detail = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("load-buildings")]
        public async Task<ActionResult<IEnumerable<BuildingDto>>> GetBuildings([FromQuery] string studentId, [FromQuery] bool isChung)
        {
            var currentStudentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentStudentId)) return Unauthorized(new { error = "Chưa xác thực!" });

            List<BuildingDto> buildings = isChung
                ? await _context.Buildings.Where(b => b.IsServerChung).OrderBy(b => b.Id).ToListAsync()
                : await _context.Buildings.Where(b => !b.IsServerChung && b.StudentId == currentStudentId).OrderBy(b => b.Id).ToListAsync();

            return Ok(buildings);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            var building = await _context.Buildings.FindAsync(id);
            if (building == null) return NotFound("Không tìm thấy công trình");
            if (building.IsServerChung) return BadRequest("Không thể xóa công trình ở server chung");

            _context.Buildings.Remove(building);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, buildingId = id });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Student loginInfo)
        {
            if (loginInfo == null || string.IsNullOrWhiteSpace(loginInfo.StudentId)) return BadRequest(new { error = "Dữ liệu trống!" });
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == loginInfo.StudentId);
            if (student == null || !BCrypt.Net.BCrypt.Verify(loginInfo.Password, student.Password))
                return Unauthorized(new { error = "Sai tài khoản hoặc mật khẩu!" });

            string tokenString = _jwtTokenService.CreateStudentToken(student);
            return Ok(new
            {
                token = tokenString,
                studentId = student.StudentId,
                fullName = student.FullName,
                vibeCoin = student.VibeCoin
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Student newUser)
        {
            if (newUser == null || string.IsNullOrWhiteSpace(newUser.StudentId)) return BadRequest(new { error = "Dữ liệu trống!" });
            if (await _context.Students.AnyAsync(s => s.StudentId == newUser.StudentId)) return BadRequest(new { error = "Username đã tồn tại!" });

            newUser.Id = 0;
            newUser.Password = BCrypt.Net.BCrypt.HashPassword(newUser.Password);
            newUser.VibeCoin = 1000; // Tặng nóng khởi nghiệp

            _context.Students.Add(newUser);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đăng ký thành công!", studentId = newUser.StudentId });
        }

        // ==========================================
        // PHASE 2: THIẾT LẬP TRANSFER MAP 1 ↔ MAP 2 (OTP CODES)
        // ==========================================

        [Authorize]
        [HttpPost("generate-ticket")]
        public async Task<IActionResult> GenerateTicket([FromBody] TransferMapRequest request)
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            var ticketCode = Guid.NewGuid().ToString("N");
            var ticket = new TeleportTicket
            {
                StudentId = studentId,
                TicketCode = ticketCode,
                TargetMap = request.TargetMap.ToLower() == "map1" ? "map1" : "map2",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(90)
            };

            _context.TeleportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, ticketCode = ticketCode, expiresInSeconds = 90 });
        }

        [AllowAnonymous]
        [HttpPost("verify-ticket")]
        public async Task<IActionResult> VerifyTicket([FromBody] VerifyTicketRequest request)
        {
            var ticket = await _context.TeleportTickets
                .FirstOrDefaultAsync(t => t.TicketCode == request.TicketCode && t.ConsumedAt == null);

            if (ticket == null) return BadRequest(new { message = "Vé không tồn tại hoặc đã sử dụng!" });
            if (ticket.ExpiresAt < DateTime.UtcNow) return BadRequest(new { message = "Vé đã hết hạn!" });
            if (ticket.TargetMap != request.TargetMap.ToLower()) return BadRequest(new { message = "Vé không dành cho Map này!" });

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == ticket.StudentId);
            if (student == null) return NotFound(new { message = "Không tìm thấy sinh viên!" });

            ticket.ConsumedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Ký JWT mới chính chủ để tiếp tục chơi Map mới
            string token = _jwtTokenService.CreateStudentToken(student);

            return Ok(new
            {
                success = true,
                token = token,
                studentId = student.StudentId,
                fullName = student.FullName,
                major = student.Major,
                vibeCoin = student.VibeCoin
            });
        }

        // ==========================================
        // PHASE 3: SHOP GỘP MUA + ĐẶT THÚ (KHÔNG QUA INVENTORY)
        // ==========================================

        private static readonly Dictionary<string, int> ItemPrices = new()
        {
            { "CHICKEN", 150 },
            { "BEAR_GUARD", 600 },
            { "RAID_TICKET", 100 },
            { "SECURITY_LOCK", 300 }
        };

        [Authorize]
        [HttpGet("shop-items")]
        public IActionResult GetShopCatalog()
        {
            var catalog = ItemPrices.Select(kp => new {
                itemCode = kp.Key,
                price = kp.Value,
                displayName = kp.Key switch
                {
                    "CHICKEN" => "Gà đẻ trứng vàng",
                    "BEAR_GUARD" => "Gấu bảo vệ",
                    "RAID_TICKET" => "Vé đột kích nhà",
                    "SECURITY_LOCK" => "Ổ khóa bảo mật",
                    _ => kp.Key
                }
            });
            return Ok(catalog);
        }

        [Authorize]
        [HttpPost("purchase-item")]
        public async Task<IActionResult> PurchaseItem([FromBody] ShopPurchaseRequest request)
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            if (!ItemPrices.TryGetValue(request.ItemCode, out int pricePerUnit))
                return BadRequest(new { success = false, message = "Vật phẩm không tồn tại!" });

            int totalCost = pricePerUnit * request.Quantity;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
            if (student == null) return NotFound();

            if (student.VibeCoin < totalCost)
                return BadRequest(new { success = false, message = "Không đủ số dư VibeCoin!" });

            student.VibeCoin -= totalCost;

            AnimalInstance? spawnedAnimal = null;

            if (request.ItemCode == "CHICKEN" || request.ItemCode == "BEAR_GUARD")
            {
                if (request.Placement == null)
                    return BadRequest(new { success = false, message = "Thú nuôi bắt buộc phải truyền tọa độ đặt!" });

                spawnedAnimal = new AnimalInstance
                {
                    Id = Guid.NewGuid(),
                    AnimalType = request.ItemCode == "CHICKEN" ? "CHICKEN" : "BEAR",
                    OwnerStudentId = studentId,
                    HouseOwnerStudentId = studentId, // Đặt tại nhà mình
                    PosX = request.Placement.PosX,
                    PosY = request.Placement.PosY,
                    PosZ = request.Placement.PosZ,
                    RotY = request.Placement.RotY,
                    IsActive = true
                };
                _context.AnimalInstances.Add(spawnedAnimal);
            }
            else if (request.ItemCode == "SECURITY_LOCK")
            {
                var alreadyOwned = await _context.PlayerInventory
                    .AnyAsync(i => i.StudentId == studentId && i.ItemCode == "SECURITY_LOCK" && i.Quantity > 0);

                if (alreadyOwned)
                    return BadRequest(new { success = false, message = "Bạn đã sở hữu ổ khóa bảo mật rồi!" });

                var invLock = new PlayerInventoryItem
                {
                    StudentId = studentId,
                    ItemCode = "SECURITY_LOCK",
                    Quantity = 1,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.PlayerInventory.Add(invLock);
            }
            else if (request.ItemCode == "RAID_TICKET")
            {
                var invTicket = await _context.PlayerInventory
                    .FirstOrDefaultAsync(i => i.StudentId == studentId && i.ItemCode == "RAID_TICKET");

                if (invTicket == null)
                {
                    invTicket = new PlayerInventoryItem { StudentId = studentId, ItemCode = "RAID_TICKET", Quantity = request.Quantity };
                    _context.PlayerInventory.Add(invTicket);
                }
                else
                {
                    invTicket.Quantity += request.Quantity;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                vibeCoin = student.VibeCoin,
                animal = spawnedAnimal != null ? new
                {
                    animalId = spawnedAnimal.Id,
                    animalType = spawnedAnimal.AnimalType,
                    posX = spawnedAnimal.PosX,
                    posY = spawnedAnimal.PosY,
                    posZ = spawnedAnimal.PosZ,
                    rotY = spawnedAnimal.RotY
                } : null
            });
        }

        // ==========================================
        // PHASE 4: QUẢN LÝ THỰC THỂ THÚ NUÔI (ANIMALS)
        // ==========================================

        [Authorize]
        [HttpGet("house-animals/{ownerId}")]
        public async Task<IActionResult> GetHouseAnimals(string ownerId)
        {
            var animals = await _context.AnimalInstances
                .AsNoTracking()
                .Where(a => a.HouseOwnerStudentId == ownerId && a.IsActive)
                .Select(a => new {
                    animalId = a.Id,
                    animalType = a.AnimalType,
                    ownerStudentId = a.OwnerStudentId,
                    posX = a.PosX,
                    posY = a.PosY,
                    posZ = a.PosZ,
                    rotY = a.RotY
                })
                .ToListAsync();

            return Ok(new { success = true, houseOwnerStudentId = ownerId, animals });
        }

        // ==========================================
        // PHASE 5: THU NHẬP THỤ ĐỘNG CỦA GÀ
        // ==========================================

        [Authorize]
        [HttpPost("chicken-income-status")]
        public async Task<IActionResult> GetIncomeStatus([FromBody] IncomeCheckRequest request)
        {
            var income = await _incomeService.ProcessIncomeAsync(request.HouseOwnerStudentId, CancellationToken.None);
            int chickenCount = await _context.AnimalInstances.CountAsync(x =>
                x.HouseOwnerStudentId == request.HouseOwnerStudentId && x.AnimalType == "CHICKEN" && x.IsActive);

            return Ok(new
            {
                success = true,
                chickenCount = chickenCount,
                pendingCoin = income.PendingCoin
            });
        }

        [Authorize]
        [HttpPost("collect-chicken-income")]
        public async Task<IActionResult> CollectIncome([FromBody] IncomeCheckRequest request)
        {
            var collectorId = User.Identity?.Name;
            if (string.IsNullOrEmpty(collectorId)) return Unauthorized();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var income = await _incomeService.ProcessIncomeAsync(request.HouseOwnerStudentId, CancellationToken.None);
            if (income.PendingCoin <= 0)
            {
                return BadRequest(new { success = false, message = "Không có xu nào để nhặt!" });
            }

            var collector = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == collectorId);
            if (collector == null) return NotFound();

            int collectedAmount = income.PendingCoin;
            collector.VibeCoin += collectedAmount;

            income.PendingCoin = 0;
            income.LastCollectedAt = DateTime.UtcNow;
            income.LastCollectorStudentId = collectorId;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                collectedAmount = collectedAmount,
                vibeCoin = collector.VibeCoin,
                pendingCoin = 0
            });
        }

        // ==========================================
        // PHASE 6: Ổ KHÓA BẢO MẬT (SECURITY LOCK)
        // ==========================================

        [Authorize]
        [HttpGet("security-lock-status")]
        public async Task<IActionResult> GetLockStatus()
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            bool owned = await _context.PlayerInventory
                .AnyAsync(i => i.StudentId == studentId && i.ItemCode == "SECURITY_LOCK" && i.Quantity > 0);

            var lockConfig = await _context.HouseSecurityLocks.FirstOrDefaultAsync(l => l.HouseOwnerStudentId == studentId);

            return Ok(new
            {
                success = true,
                owned = owned,
                configured = lockConfig != null,
                enabled = lockConfig?.IsEnabled ?? false
            });
        }

        [Authorize]
        [HttpPost("configure-security-lock")]
        public async Task<IActionResult> ConfigureLock([FromBody] ConfigureLockRequest request)
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            bool owned = await _context.PlayerInventory
                .AnyAsync(i => i.StudentId == studentId && i.ItemCode == "SECURITY_LOCK" && i.Quantity > 0);

            if (!owned)
                return BadRequest(new { success = false, message = "Bạn cần mua ổ khóa trong Shop trước!" });

            if (request.Question.Length < 5 || request.Question.Length > 150)
                return BadRequest(new { success = false, message = "Câu hỏi phải từ 5-150 ký tự." });

            if (request.CorrectAnswer != 0 && request.CorrectAnswer != 1)
                return BadRequest(new { success = false, message = "Đáp án đúng chỉ được nhận 0 (A) hoặc 1 (B)." });

            var dbLock = await _context.HouseSecurityLocks.FirstOrDefaultAsync(l => l.HouseOwnerStudentId == studentId);
            if (dbLock == null)
            {
                dbLock = new HouseSecurityLock { HouseOwnerStudentId = studentId };
                _context.HouseSecurityLocks.Add(dbLock);
            }

            dbLock.Question = request.Question;
            dbLock.AnswerA = request.AnswerA;
            dbLock.AnswerB = request.AnswerB;
            dbLock.CorrectAnswer = request.CorrectAnswer;
            dbLock.IsEnabled = true;
            dbLock.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Cấu hình ổ khóa bảo mật thành công!" });
        }

        [Authorize]
        [HttpPost("toggle-security-lock")]
        public async Task<IActionResult> ToggleLock([FromBody] ToggleLockRequest request)
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            var dbLock = await _context.HouseSecurityLocks.FirstOrDefaultAsync(l => l.HouseOwnerStudentId == studentId);
            if (dbLock == null)
                return BadRequest(new { success = false, message = "Khóa chưa được cấu hình!" });

            dbLock.IsEnabled = request.Enabled;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, enabled = dbLock.IsEnabled });
        }

        // ==========================================
        // PHASE 7: HỆ THỐNG ĐỘT KÍCH (RAID SESSION) & CHỐNG HACK
        // ==========================================

        [Authorize]
        [HttpPost("start-random-raid")]
        public async Task<IActionResult> StartRandomRaid()
        {
            var attackerId = User.Identity?.Name;
            if (string.IsNullOrEmpty(attackerId)) return Unauthorized();

            var invTicket = await _context.PlayerInventory
                .FirstOrDefaultAsync(i => i.StudentId == attackerId && i.ItemCode == "RAID_TICKET");

            if (invTicket == null || invTicket.Quantity <= 0)
                return BadRequest(new { success = false, message = "Bạn không có Vé Đột Kích (Raid Ticket)!" });

            // Tìm ngẫu nhiên một nhà nạn nhân thỏa mãn:
            // 1. Không phải nhà của chính mình
            // 2. Nhà đó phải có nhiều hơn 2 con gà sống thực tế trong DB
            var potentialTargets = await _context.Students
                .Where(s => s.StudentId != attackerId)
                .ToListAsync();

            string? targetStudentId = null;
            foreach (var potential in potentialTargets)
            {
                int chickenCount = await _context.AnimalInstances.CountAsync(x =>
                    x.HouseOwnerStudentId == potential.StudentId && x.AnimalType == "CHICKEN" && x.IsActive);

                if (chickenCount > 2)
                {
                    targetStudentId = potential.StudentId;
                    break; // Ưu tiên chọn nhà đầu tiên hợp lệ (có thể xáo trộn ngẫu nhiên list nếu muốn)
                }
            }

            if (string.IsNullOrEmpty(targetStudentId))
            {
                return BadRequest(new { success = false, message = "Không tìm thấy nhà nào có nhiều hơn 2 con gà để đột kích!" });
            }

            var targetUser = await _context.Students.FirstAsync(s => s.StudentId == targetStudentId);

            // Xác định xem nhà nạn nhân có ổ khóa hoạt động hay không
            var targetLock = await _context.HouseSecurityLocks
                .FirstOrDefaultAsync(l => l.HouseOwnerStudentId == targetStudentId && l.IsEnabled);

            bool lockRequired = targetLock != null;

            // Tiến hành trừ vé đột kích của kẻ công kích
            invTicket.Quantity -= 1;

            // Tạo phiên Raid mới
            var session = new RaidSession
            {
                Id = Guid.NewGuid(),
                AttackerStudentId = attackerId,
                TargetStudentId = targetStudentId,
                Status = lockRequired ? "AWAITING_LOCK" : "ACTIVE",
                LockRequired = lockRequired,
                LockPassed = !lockRequired,
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5) // Phiên đột kích hết hạn sau 5 phút
            };

            _context.RaidSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                raidSessionId = session.Id,
                targetStudentId = targetStudentId,
                targetFullName = targetUser.FullName,
                lockRequired = lockRequired,
                expiresAt = session.ExpiresAt
            });
        }

        [Authorize]
        [HttpGet("raid/{sessionId}/security-question")]
        public async Task<IActionResult> GetRaidQuestion(Guid sessionId)
        {
            var session = await _context.RaidSessions.FindAsync(sessionId);
            if (session == null || session.Status != "AWAITING_LOCK")
                return BadRequest("Phiên đột kích không hợp lệ hoặc không cần trả lời câu hỏi!");

            var targetLock = await _context.HouseSecurityLocks
                .FirstOrDefaultAsync(l => l.HouseOwnerStudentId == session.TargetStudentId);

            if (targetLock == null) return NotFound("Không tìm thấy cấu hình khóa.");

            // BẢO MẬT TUYỆT ĐỐI: Tuyệt đối không được trả đáp án đúng (CorrectAnswer) về Client!
            return Ok(new
            {
                success = true,
                question = targetLock.Question,
                answerA = targetLock.AnswerA,
                answerB = targetLock.AnswerB
            });
        }

        [Authorize]
        [HttpPost("raid/{sessionId}/answer-security")]
        public async Task<IActionResult> AnswerRaidQuestion(Guid sessionId, [FromBody] AnswerSecurityRequest request)
        {
            var session = await _context.RaidSessions.FindAsync(sessionId);
            if (session == null || session.Status != "AWAITING_LOCK")
                return BadRequest("Phiên đột kích không hợp lệ!");

            var targetLock = await _context.HouseSecurityLocks
                .FirstOrDefaultAsync(l => l.HouseOwnerStudentId == session.TargetStudentId);

            if (targetLock == null) return NotFound();

            if (targetLock.CorrectAnswer == request.SelectedAnswer)
            {
                // TRẢ LỜI ĐÚNG: Kích hoạt phiên đột kích thành ACTIVE để bắt đầu bắt gà
                session.LockPassed = true;
                session.Status = "ACTIVE";
                await _context.SaveChangesAsync();

                return Ok(new { success = true, correct = true, authorized = true });
            }
            else
            {
                // TRẢ LỜI SAI: Khóa phiên, cho phép rút lui về nhà sau 10 giây
                session.Status = "FAILED_LOCK";
                session.FailedReturnAt = DateTime.UtcNow.AddSeconds(10);
                session.EndedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, correct = false, authorized = false, returnAfterSeconds = 10 });
            }
        }

        // ==========================================
        // PHASE 8: THỰC THI TRỘM GÀ (STEAL CHICKEN)
        // ==========================================

        [Authorize]
        [HttpPost("raid/{sessionId}/steal-chicken")]
        public async Task<IActionResult> StealChicken(Guid sessionId, [FromBody] StealChickenRequest request)
        {
            var attackerId = User.Identity?.Name;
            if (string.IsNullOrEmpty(attackerId)) return Unauthorized();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var session = await _context.RaidSessions.FindAsync(sessionId);
            if (session == null || session.Status != "ACTIVE" || session.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { success = false, message = "Phiên đột kích không tồn tại, đã hết hạn hoặc đã kết thúc!" });

            if (session.AttackerStudentId != attackerId)
                return Unauthorized();

            // Tìm con gà cụ thể của nạn nhân
            var chicken = await _context.AnimalInstances
                .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.HouseOwnerStudentId == session.TargetStudentId && a.AnimalType == "CHICKEN" && a.IsActive);

            if (chicken == null)
                return BadRequest(new { success = false, message = "Con gà này vừa bị người khác trộm mất hoặc không tồn tại!" });

            // THỰC THI CHUYỂN QUYỀN SỞ HỮU:
            // Biến gà thành của kẻ trộm, đặt ở trạng thái chờ đặt (hoặc tọa độ mặc định ở nhà kẻ trộm)
            chicken.OwnerStudentId = attackerId;
            chicken.HouseOwnerStudentId = attackerId; // Đưa về vườn nhà kẻ trộm
            chicken.PosX = 0; // Tọa độ mặc định chờ người chơi sắp xếp lại sau
            chicken.PosY = 0;
            chicken.PosZ = 0;
            chicken.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            int targetRemaining = await _context.AnimalInstances.CountAsync(x => x.HouseOwnerStudentId == session.TargetStudentId && x.AnimalType == "CHICKEN" && x.IsActive);
            int attackerTotal = await _context.AnimalInstances.CountAsync(x => x.HouseOwnerStudentId == attackerId && x.AnimalType == "CHICKEN" && x.IsActive);

            return Ok(new
            {
                success = true,
                stolenAnimalId = chicken.Id,
                targetRemainingChickenCount = targetRemaining,
                attackerChickenCount = attackerTotal
            });
        }

        // ==========================================
        // PHASE 9: CHẾT TRONG RAID (DEATH / TIMEOUT FAIL)
        // ==========================================

        [Authorize]
        [HttpPost("raid/{sessionId}/fail")]
        public async Task<IActionResult> FailRaid(Guid sessionId, [FromBody] FailRaidRequest request)
        {
            var attackerId = User.Identity?.Name;
            if (string.IsNullOrEmpty(attackerId)) return Unauthorized();

            var session = await _context.RaidSessions.FindAsync(sessionId);
            if (session == null || session.AttackerStudentId != attackerId)
                return BadRequest("Phiên đột kích không hợp lệ.");

            session.Status = "FAILED";
            session.EndedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, returnTarget = "map1", reason = request.Reason });
        }

        // ==========================================
        // PHASE 10: CONTEXT CHO GẤU BẢO VỆ AI
        // ==========================================

        [Authorize]
        [HttpGet("raid/{sessionId}/context")]
        public async Task<IActionResult> GetRaidContext(Guid sessionId)
        {
            var session = await _context.RaidSessions.FindAsync(sessionId);
            if (session == null) return NotFound();

            return Ok(new
            {
                success = true,
                visitorStudentId = session.AttackerStudentId,
                houseOwnerStudentId = session.TargetStudentId,
                authorized = session.Status == "ACTIVE"
            });
        }

        // ==========================================
        // PHASE 11: THƯỞNG CỔNG BÌNH MINH & BOOTSTRAP MAP 2
        // ==========================================

        [Authorize]
        [HttpPost("claim-dawn-reward")]
        public async Task<IActionResult> ClaimDawnReward([FromBody] ClaimDawnGateRequest body, CancellationToken cancellationToken)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.NightCycleId))
                return BadRequest(new { success = false, message = "Thiếu mã chu kỳ ban đêm." });

            string? studentId = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            bool alreadyClaimed = await _context.DawnGateRewards.AnyAsync(x => x.StudentId == studentId && x.NightCycleId == body.NightCycleId, cancellationToken);

            var student = await _context.Students.FirstOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
            if (student == null) return NotFound();

            if (alreadyClaimed)
            {
                return Ok(new { success = true, alreadyClaimed = true, rewardAmount = 0, vibeCoin = student.VibeCoin, message = "Phần thưởng đã nhận." });
            }

            student.VibeCoin += 50;
            _context.DawnGateRewards.Add(new DawnGateReward { StudentId = studentId, NightCycleId = body.NightCycleId, RewardAmount = 50, ClaimedAt = DateTime.UtcNow });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { success = true, alreadyClaimed = false, rewardAmount = 50, vibeCoin = student.VibeCoin });
        }

        [Authorize]
        [HttpGet("map2-bootstrap")]
        public async Task<IActionResult> GetMap2Bootstrap()
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId);
            if (student == null) return NotFound();

            var ticketCount = await _context.PlayerInventory
                .Where(i => i.StudentId == studentId && i.ItemCode == "RAID_TICKET")
                .Select(i => i.Quantity)
                .FirstOrDefaultAsync();

            var lockConfig = await _context.HouseSecurityLocks.AsNoTracking().FirstOrDefaultAsync(l => l.HouseOwnerStudentId == studentId);
            int chickenCount = await _context.AnimalInstances.CountAsync(x => x.HouseOwnerStudentId == studentId && x.AnimalType == "CHICKEN" && x.IsActive);

            var income = await _incomeService.ProcessIncomeAsync(studentId, CancellationToken.None);

            return Ok(new
            {
                success = true,
                profile = new
                {
                    studentId = student.StudentId,
                    fullName = student.FullName,
                    major = student.Major,
                    vibeCoin = student.VibeCoin
                },
                inventory = new
                {
                    raidTickets = ticketCount,
                    securityLockOwned = lockConfig != null
                },
                house = new
                {
                    securityLockConfigured = lockConfig != null,
                    securityLockEnabled = lockConfig?.IsEnabled ?? false,
                    chickenCount = chickenCount,
                    pendingChickenCoin = income.PendingCoin
                }
            });
        }
    }

    // ==========================================
    // DTO REQUEST CLASSES
    // ==========================================

    public sealed class TransferMapRequest { public string TargetMap { get; set; } = "map2"; }
    public sealed class VerifyTicketRequest { public string TicketCode { get; set; } = string.Empty; public string TargetMap { get; set; } = "map2"; }
    public sealed class ShopPurchaseRequest { public string ItemCode { get; set; } = string.Empty; public int Quantity { get; set; } = 1; public AnimalPlacementRequest? Placement { get; set; } }
    public sealed class AnimalPlacementRequest { public double PosX { get; set; } public double PosY { get; set; } public double PosZ { get; set; } public double RotY { get; set; } }
    public sealed class IncomeCheckRequest { public string HouseOwnerStudentId { get; set; } = string.Empty; }
    public sealed class ConfigureLockRequest { public string Question { get; set; } = string.Empty; public string AnswerA { get; set; } = string.Empty; public string AnswerB { get; set; } = string.Empty; public int CorrectAnswer { get; set; } = 0; }
    public sealed class ToggleLockRequest { public bool Enabled { get; set; } }
    public sealed class AnswerSecurityRequest { public int SelectedAnswer { get; set; } }
    public sealed class StealChickenRequest { public Guid AnimalId { get; set; } }
    public sealed class FailRaidRequest { public string Reason { get; set; } = "PLAYER_DIED"; }
    public sealed class ClaimDawnGateRequest { public string NightCycleId { get; set; } = string.Empty; }
}