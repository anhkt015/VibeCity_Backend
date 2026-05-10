using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VibeCity_API.Data;

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
        public string Timestamp { get; set; } = string.Empty;
    }
    public class Student
    {
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
    }
}