using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VibeCity_API.Data;

namespace VibeCity_API.Data
{
    [Table("Buildings")]
    public class BuildingDto
    {
        [Key]
        public int Id { get; set; }
        public int BuildingType { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public double RotY { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Column("StudentId")]
        public string? StudentId { get; set; } = string.Empty;

        [Column("IsServerChung")]
        public bool IsServerChung { get; set; } = true;
    }

    // PHASE 2 — Thêm VibeCoin vào class Student
    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("StudentId")]
        public string StudentId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public double Gpa { get; set; }

        [Column("unlocked_skills")]
        public string? UnlockedSkills { get; set; } = string.Empty;

        [Column("survived_days")]
        public int SurvivedDays { get; set; }

        [Column("total_survival_minutes")]
        public double TotalSurvivalMinutes { get; set; }

        [Column("vibe_coin")]
        public int VibeCoin { get; set; } = 1000; // Mặc định 1000 VibeCoin
    }

    public class NpcDto
    {
        [Key]
        public int Id { get; set; }
        [Column("npctype")]
        public string NpcType { get; set; } = "Teacher";
        public double SpawnX { get; set; }
        public double SpawnY { get; set; }
        public double SpawnZ { get; set; }
        public double InteractionRadius { get; set; } = 3.0;
    }

    public class LessonDto
    {
        [Key]
        public int Id { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string QuizJson { get; set; } = string.Empty;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class BuildingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public BuildingController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // 1. API Xây nhà (POST) - ĐÃ BẢO MẬT
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BuildingDto data)
        {
            if (data == null)
                return BadRequest(new { error = "Dữ liệu gửi sang bị trống!" });

            var currentStudentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentStudentId))
            {
                return Unauthorized(new { error = "Không tìm thấy thông tin xác thực từ Token!" });
            }

            data.StudentId = currentStudentId;

            try
            {
                data.Id = 0;
                data.Timestamp = DateTime.UtcNow;

                _context.Buildings.Add(data);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ [POST] Sinh viên '{data.StudentId}' đã xây nhà loại {data.BuildingType} ở Chế độ Server Chung = {data.IsServerChung}!");

                return Ok(new
                {
                    message = "Lưu thành công!",
                    id = data.Id,
                    type = data.BuildingType,
                    posX = data.PosX,
                    posY = data.PosY,
                    posZ = data.PosZ,
                    rotY = data.RotY,
                    timestamp = data.Timestamp,
                    studentId = data.StudentId,
                    isServerChung = data.IsServerChung,
                    status = "Success"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [POST] Lỗi khi lưu: {ex.Message}");
                return StatusCode(500, new
                {
                    error = "Lỗi server khi lưu nhà!",
                    detail = ex.Message
                });
            }
        }

        // 2. API Tải dữ liệu nhà (GET) - ĐÃ BẢO MẬT MAP CÁ NHÂN
        [Authorize]
        [HttpGet("load-buildings")]
        public async Task<ActionResult<IEnumerable<BuildingDto>>> GetBuildings([FromQuery] string studentId, [FromQuery] bool isChung)
        {
            try
            {
                var currentStudentId = User.Identity?.Name;
                if (string.IsNullOrEmpty(currentStudentId))
                {
                    return Unauthorized(new { error = "Không tìm thấy thông tin xác thực từ Token!" });
                }

                List<BuildingDto> buildings;

                if (isChung)
                {
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == true)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - SERVER CHUNG] Đã nạp thành công {buildings.Count} căn nhà của toàn trường gửi cho Unity.");
                }
                else
                {
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == false && b.StudentId == currentStudentId)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - MAP CÁ NHÂN] Đã nạp thành công {buildings.Count} căn nhà riêng của sinh viên '{currentStudentId}'.");
                }

                return Ok(buildings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [GET] Lỗi khi lấy dữ liệu: {ex.Message}");
                return StatusCode(500, new
                {
                    error = "Server không lấy được dữ liệu nhà!",
                    detail = ex.Message
                });
            }
        }

        // 3. API Xóa công trình (DELETE)
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            var building = await _context.Buildings.FindAsync(id);

            if (building == null)
                return NotFound("Không tìm thấy công trình");

            if (building.IsServerChung)
                return BadRequest("Không thể xóa công trình ở server chung");

            _context.Buildings.Remove(building);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                buildingId = id
            });
        }

        // 4. API Đăng nhập - PHASE 3: Trả VibeCoin khi Login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Student loginInfo)
        {
            if (loginInfo == null)
                return BadRequest(new { error = "Request body null!" });

            if (string.IsNullOrWhiteSpace(loginInfo.StudentId) || string.IsNullOrWhiteSpace(loginInfo.Password))
            {
                return BadRequest(new { error = "Vui lòng nhập đầy đủ Username và mật khẩu!" });
            }

            var student = await _context.Students
             .FirstOrDefaultAsync(s => s.StudentId == loginInfo.StudentId);

            if (student == null)
                return Unauthorized(new { error = "Sai Username hoặc mật khẩu!" });

            bool ok = BCrypt.Net.BCrypt.Verify(loginInfo.Password, student.Password);

            if (!ok)
                return Unauthorized(new { error = "Sai Username hoặc mật khẩu!" });

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = Environment.GetEnvironmentVariable("Jwt_Key") ?? _configuration["Jwt:Key"] ?? "Key_Mac_Dinh_Sieu_Bao_Mat_VibeCity_2026";
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, student.StudentId),
                    new Claim("StudentId", student.StudentId),
                    new Claim("FullName", student.FullName),
                    new Claim("Major", student.Major),
                    new Claim("Gpa", student.Gpa.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            Console.WriteLine($"✅ Sinh viên {student.FullName} đã đăng nhập thành công và cấp Token.");

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                token = tokenString,
                studentId = student.StudentId,
                fullName = student.FullName,
                major = student.Major,
                gpa = student.Gpa,
                unlockedSkills = student.UnlockedSkills,
                survivedDays = student.SurvivedDays,
                totalSurvivalMinutes = student.TotalSurvivalMinutes,
                vibeCoin = student.VibeCoin // Đã thêm ở Phase 3
            });
        }

        // 5. API Kiểm tra tài khoản
        [HttpGet("check")]
        public async Task<IActionResult> CheckUser([FromQuery] string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                return BadRequest(new { error = "Thiếu studentId!" });

            bool exists = await _context.Students
                .AnyAsync(s => s.StudentId == studentId);

            return Ok(new { exists });
        }

        // 6. API Đăng ký tài khoản - PHASE 4: Gán tiền mặc định khi đăng ký
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Student newUser)
        {
            if (newUser == null)
                return BadRequest(new { error = "Request body null!" });

            newUser.StudentId = newUser.StudentId?.Trim() ?? "";
            newUser.Password = newUser.Password ?? "";
            newUser.FullName = newUser.FullName?.Trim() ?? "";
            newUser.Major = newUser.Major?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(newUser.StudentId))
                return BadRequest(new { error = "Thiếu Username/StudentId!" });

            if (string.IsNullOrWhiteSpace(newUser.Password))
                return BadRequest(new { error = "Thiếu mật khẩu!" });

            if (string.IsNullOrWhiteSpace(newUser.FullName))
                return BadRequest(new { error = "Thiếu họ tên!" });

            if (string.IsNullOrWhiteSpace(newUser.Major))
                return BadRequest(new { error = "Thiếu ngành học!" });

            if (newUser.Gpa < 0 || newUser.Gpa > 4)
                return BadRequest(new { error = "GPA phải nằm trong khoảng 0 - 4!" });

            bool exists = await _context.Students
                .AnyAsync(s => s.StudentId == newUser.StudentId);

            if (exists)
                return BadRequest(new { error = "Username/StudentId này đã được đăng ký rồi!" });

            try
            {
                newUser.Id = 0;
                newUser.Password = BCrypt.Net.BCrypt.HashPassword(newUser.Password);
                newUser.VibeCoin = 1000; // Đã thêm ở Phase 4

                _context.Students.Add(newUser);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Đăng ký thành công: {newUser.StudentId} - {newUser.FullName}");

                return Ok(new
                {
                    message = "Đăng ký thành công!",
                    studentId = newUser.StudentId,
                    fullName = newUser.FullName,
                    major = newUser.Major,
                    gpa = newUser.Gpa,
                    vibeCoin = newUser.VibeCoin // Đã thêm ở Phase 4
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Lỗi khi đăng ký tài khoản!",
                    detail = ex.Message
                });
            }
        }

        // 7. API Mở khóa kỹ năng (POST) - ĐÃ BẢO MẬT
        [Authorize]
        [HttpPost("unlock-skill")]
        public async Task<IActionResult> UnlockSkill([FromBody] SkillUpdateRequest request)
        {
            try
            {
                var currentStudentId = User.Identity?.Name;
                if (string.IsNullOrEmpty(currentStudentId))
                {
                    return Unauthorized(new { error = "Không tìm thấy thông tin xác thực từ Token!" });
                }

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == currentStudentId);
                if (student == null)
                    return NotFound(new { error = "Không tìm thấy sinh viên!" });

                if (string.IsNullOrEmpty(student.UnlockedSkills))
                {
                    student.UnlockedSkills = request.SkillId.ToString();
                }
                else
                {
                    var existingSkills = student.UnlockedSkills.Split(',').ToList();
                    if (!existingSkills.Contains(request.SkillId.ToString()))
                    {
                        existingSkills.Add(request.SkillId.ToString());
                        student.UnlockedSkills = string.Join(",", existingSkills);
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, currentSkills = student.UnlockedSkills });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server!", detail = ex.Message });
            }
        }

        // 8. API Tích lũy tiến trình ngày đêm khi thoát game
        [Authorize]
        [HttpPost("complete-night")]
        public async Task<IActionResult> CompleteNight([FromBody] QuitGameProgressRequest request)
        {
            try
            {
                var studentId = User.Identity?.Name;
                if (string.IsNullOrEmpty(studentId))
                {
                    return Unauthorized(new { error = "Không tìm thấy thông tin sinh viên từ Token!" });
                }

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
                if (student == null)
                {
                    return NotFound(new { error = "Sinh viên không tồn tại trong hệ thống VibeCity!" });
                }

                double actualMinutes = Math.Abs(request.SurvivalTime);
                string timeStatusText = request.SurvivalTime < 0
                    ? $"BAN ĐÊM (Nhận số âm: {request.SurvivalTime}p)"
                    : $"BAN NGÀY (Nhận số dương: {request.SurvivalTime}p)";

                Console.WriteLine($"[VibeCity] Sinh viên {studentId} tắt game lúc {timeStatusText}. Thời gian chơi session này: {actualMinutes} phút.");

                student.SurvivedDays += request.SurvivedDays;
                student.TotalSurvivalMinutes += actualMinutes;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đã tích lũy số ngày và số phút sinh tồn khi tắt game thành công!",
                    savedMinutes = actualMinutes,
                    totalDays = student.SurvivedDays,
                    totalSurvivalMinutes = student.TotalSurvivalMinutes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi xử lý hệ thống ngày đêm tại Server!", detail = ex.Message });
            }
        }

        // 9. API Ẩn đánh thức Server Render
        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult PingServer()
        {
            Console.WriteLine("⏰ [WakeUp] Unity vừa gọi API Ping để đánh thức Server!");
            return Ok(new
            {
                success = true,
                message = "Pong! Server VibeCity đã sẵn sàng hoạt động."
            });
        }

        // PHASE 5 — API lấy tài khoản và VibeCoin cho Map 2
        [Authorize]
        [HttpGet("map2-profile")]
        public async Task<IActionResult> GetMap2Profile()
        {
            try
            {
                // StudentId được đọc trực tiếp từ JWT của người chơi
                string? studentId = User.Identity?.Name;

                if (string.IsNullOrWhiteSpace(studentId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        error = "Không đọc được StudentId từ JWT."
                    });
                }

                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.StudentId == studentId);

                if (student == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = "Không tìm thấy tài khoản sinh viên."
                    });
                }

                return Ok(new
                {
                    success = true,
                    studentId = student.StudentId,
                    fullName = student.FullName,
                    major = student.Major,
                    gpa = student.Gpa,
                    vibeCoin = student.VibeCoin
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Map2 Profile] Error: " + ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = "Server không tải được tài khoản Map 2."
                });
            }
        }
    }

    public class SkillUpdateRequest
    {
        public int SkillId { get; set; }
    }

    public class QuitGameProgressRequest
    {
        public int SurvivedDays { get; set; }
        public double SurvivalTime { get; set; }
    }
}