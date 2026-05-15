using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VibeCity_API.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeCity_API.Data
{
    // --- Model chứa dữ liệu nhà (Đã fix lỗi Double & Null) ---
    public class BuildingDto
    {
        [Key]
        public int Id { get; set; }
        public int BuildingType { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public double RotY { get; set; }

        // Gán mặc định là chuỗi rỗng để tránh lỗi Warning Null
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;// MSSV của Nhật Anh
        public string FullName { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;     // Ngành học (VD: Robot & AI)
        public double Gpa { get; set; }       // Để AI biết ông học giỏi hay không mà động viên
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
        public string QuizJson { get; set; } = string.Empty; // Lưu 5 câu hỏi trắc nghiệm
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

        // 1. API Xây nhà (POST) - Nhật Anh dùng cái này để Lưu
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BuildingDto data)
        {
            if (data == null) return BadRequest("Dữ liệu từ Nhật Anh gửi sang bị trống!");

            try
            {
                // Thêm vào Database
                data.Timestamp = DateTime.UtcNow;
                _context.Buildings.Add(data);

                // Đợi SQL lưu xong để lấy được cái ID tự động sinh ra
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ [POST] Đã lưu nhà loại {data.BuildingType} thành công vào SQL!");

                // Trả về cho Unity toàn bộ thông tin nhà kèm theo ID thực tế
                return Ok(new
                {
                    message = "Lưu thành công!",
                    id = data.Id,
                    type = data.BuildingType,
                    status = "Success"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [POST] Lỗi khi lưu: {ex.Message}");
                return StatusCode(500, "Lỗi Server rồi ní ơi!");
            }
        }

        // 2. API Lấy danh sách nhà (GET) - Nhật Anh dùng cái này để Load bản đồ
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BuildingDto>>> GetBuildings()
        {
            try
            {
                // Lấy danh sách nhà, sắp xếp theo ID để load cho đúng thứ tự xây
                var buildings = await _context.Buildings
                                              .OrderBy(b => b.Id)
                                              .ToListAsync();

                Console.WriteLine($"🔍 [GET] Đã lấy {buildings.Count} căn nhà gửi cho Nhật Anh.");
                return Ok(buildings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [GET] Lỗi khi lấy dữ liệu: {ex.Message}");
                return StatusCode(500, "Server không nhả dữ liệu được!");
            }
        }
        // Endpoint Đăng nhập sử dụng trực tiếp class Student
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Student loginInfo)
        {
            // Kiểm tra đầu vào
            if (string.IsNullOrEmpty(loginInfo.StudentId) || string.IsNullOrEmpty(loginInfo.Password))
            {
                return BadRequest(new { message = "Vui lòng nhập đầy đủ MSSV và mật khẩu!" });
            }

            // Tìm sinh viên trong bảng Students khớp cả MSSV và mật khẩu
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentId == loginInfo.StudentId && s.Password == loginInfo.Password);

            if (student == null)
            {
                // Trả về lỗi 401 nếu không khớp
                return Unauthorized(new { message = "Sai mã số sinh viên hoặc mật khẩu!" });
            }

            // Đăng nhập thành công: Gửi toàn bộ Profile về cho Unity
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
        // 1. API Kiểm tra User tồn tại chưa (Để Unity đổi Mode Login/Register)
        [HttpGet("check")]
        public async Task<IActionResult> CheckUser(string studentId)
        {
            var exists = await _context.Students.AnyAsync(s => s.StudentId == studentId);
            return Ok(new { exists = exists });
        }

        // 2. API Đăng ký tài khoản mới
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Student newUser)
        {
            // Bước 1: Kiểm tra xem MSSV này đã tồn tại chưa
            if (await _context.Students.AnyAsync(s => s.StudentId == newUser.StudentId))
            {
                return BadRequest(new { error = "Mã số sinh viên này đã được đăng ký rồi!" });
            }

            try
            {
                newUser.Id = 0; // QUAN TRỌNG: Đảm bảo ID bằng 0 để DB tự tăng, tránh trùng PK
                _context.Students.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Đăng ký thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi trùng khóa hoặc lỗi hệ thống!", detail = ex.Message });
            }
        }
    }
}