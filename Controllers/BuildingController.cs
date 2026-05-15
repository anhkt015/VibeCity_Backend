using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VibeCity_API.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeCity_API.Data
{
    // --- Model chứa dữ liệu nhà ---
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
    }

    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;

        public double Gpa { get; set; }
    }

    public class NpcDto
    {
        [Key]
        public int Id { get; set; }

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

        public BuildingController(AppDbContext context)
        {
            _context = context;
        }

        // 1. API Xây nhà (POST)
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BuildingDto data)
        {
            if (data == null)
                return BadRequest(new { error = "Dữ liệu gửi sang bị trống!" });

            try
            {
                data.Id = 0;
                data.Timestamp = DateTime.UtcNow;

                _context.Buildings.Add(data);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ [POST] Đã lưu nhà loại {data.BuildingType} thành công vào SQL!");

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

        // 2. API Lấy danh sách nhà (GET)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BuildingDto>>> GetBuildings()
        {
            try
            {
                var buildings = await _context.Buildings
                    .OrderBy(b => b.Id)
                    .ToListAsync();

                Console.WriteLine($"🔍 [GET] Đã lấy {buildings.Count} căn nhà gửi cho Unity.");
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

        // 3. API Đăng nhập
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Student loginInfo)
        {
            if (loginInfo == null)
                return BadRequest(new { error = "Request body null!" });

            if (string.IsNullOrWhiteSpace(loginInfo.StudentId) ||
                string.IsNullOrWhiteSpace(loginInfo.Password))
            {
                return BadRequest(new { error = "Vui lòng nhập đầy đủ Username và mật khẩu!" });
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    s.StudentId == loginInfo.StudentId &&
                    s.Password == loginInfo.Password);

            if (student == null)
                return Unauthorized(new { error = "Sai Username hoặc mật khẩu!" });

            Console.WriteLine($"✅ Sinh viên {student.FullName} đã đăng nhập thành công.");

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                studentId = student.StudentId,
                fullName = student.FullName,
                major = student.Major,
                gpa = student.Gpa
            });
        }

        // 4. API Kiểm tra User tồn tại chưa
        [HttpGet("check")]
        public async Task<IActionResult> CheckUser([FromQuery] string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                return BadRequest(new { error = "Thiếu studentId!" });

            bool exists = await _context.Students
                .AnyAsync(s => s.StudentId == studentId);

            return Ok(new { exists });
        }

        // 5. API Đăng ký tài khoản mới
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

                _context.Students.Add(newUser);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Đăng ký thành công: {newUser.StudentId} - {newUser.FullName}");

                return Ok(new
                {
                    message = "Đăng ký thành công!",
                    studentId = newUser.StudentId,
                    fullName = newUser.FullName,
                    major = newUser.Major,
                    gpa = newUser.Gpa
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
    }
}
