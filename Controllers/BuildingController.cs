using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using VibeCity_API.Data;
using VibeCity_API.Services;

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
        public int VibeCoin { get; set; } = 1000;
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
        private readonly IJwtTokenService _jwtTokenService;

        public BuildingController(AppDbContext context, IConfiguration configuration, IJwtTokenService jwtTokenService)
        {
            _context = context;
            _configuration = configuration;
            _jwtTokenService = jwtTokenService;
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
                return StatusCode(500, new { error = "Lỗi server khi lưu nhà!", detail = ex.Message });
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
                }
                else
                {
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == false && b.StudentId == currentStudentId)
                        .OrderBy(b => b.Id)
                        .ToListAsync();
                }

                return Ok(buildings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server không lấy được dữ liệu nhà!", detail = ex.Message });
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

            return Ok(new { success = true, buildingId = id });
        }

        // 4. API Đăng nhập - SỬ DỤNG DỊCH VỤ JWT CHUNG
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Student loginInfo)
        {
            if (loginInfo == null)
                return BadRequest(new { error = "Dữ liệu trống!" });

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

            // Ký Token bằng JwtTokenService chung
            string tokenString = _jwtTokenService.CreateStudentToken(student);

            Console.WriteLine($"✅ Sinh viên {student.FullName} đăng nhập thành công.");

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
                vibeCoin = student.VibeCoin
            });
        }

        // 5. API Kiểm tra tài khoản
        [HttpGet("check")]
        public async Task<IActionResult> CheckUser([FromQuery] string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                return BadRequest(new { error = "Thiếu studentId!" });

            bool exists = await _context.Students.AnyAsync(s => s.StudentId == studentId);
            return Ok(new { exists });
        }

        // 6. API Đăng ký tài khoản - Gán tiền mặc định
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Student newUser)
        {
            if (newUser == null)
                return BadRequest(new { error = "Dữ liệu trống!" });

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

            bool exists = await _context.Students.AnyAsync(s => s.StudentId == newUser.StudentId);
            if (exists)
                return BadRequest(new { error = "Username này đã được sử dụng!" });

            try
            {
                newUser.Id = 0;
                newUser.Password = BCrypt.Net.BCrypt.HashPassword(newUser.Password);
                newUser.VibeCoin = 1000; // Tiền mặc định

                _context.Students.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Đăng ký thành công!",
                    studentId = newUser.StudentId,
                    fullName = newUser.FullName,
                    major = newUser.Major,
                    gpa = newUser.Gpa,
                    vibeCoin = newUser.VibeCoin
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi đăng ký tài khoản!", detail = ex.Message });
            }
        }

        // 7. API Mở khóa kỹ năng (POST)
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

        // 8. API Tích lũy tiến trình ngày đêm
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
                    return NotFound(new { error = "Sinh viên không tồn tại!" });
                }

                double actualMinutes = Math.Abs(request.SurvivalTime);
                student.SurvivedDays += request.SurvivedDays;
                student.TotalSurvivalMinutes += actualMinutes;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đã tích lũy tiến trình thành công!",
                    savedMinutes = actualMinutes,
                    totalDays = student.SurvivedDays,
                    totalSurvivalMinutes = student.TotalSurvivalMinutes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi xử lý hệ thống ngày đêm!", detail = ex.Message });
            }
        }

        // 9. API Ping đánh thức
        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult PingServer()
        {
            return Ok(new { success = true, message = "Pong! Server VibeCity đã sẵn sàng hoạt động." });
        }

        // API tải lại hồ sơ độc lập của Map 2
        [Authorize]
        [HttpGet("map2-profile")]
        public async Task<IActionResult> GetMap2Profile()
        {
            try
            {
                string? studentId = User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(studentId))
                {
                    return Unauthorized(new { success = false, error = "Không đọc được thông tin xác thực." });
                }

                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.StudentId == studentId);

                if (student == null)
                {
                    return NotFound(new { success = false, error = "Không tìm thấy sinh viên." });
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
                return StatusCode(500, new { success = false, error = "Lỗi server!", detail = ex.Message });
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