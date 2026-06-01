/*
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
        public string StudentId { get; set; } = string.Empty; // Ai là người xây căn này?
        public bool IsServerChung { get; set; } = true;
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

            if (string.IsNullOrEmpty(data.StudentId))
                return BadRequest(new { error = "Không thể xây nhà do thiếu tài khoản StudentId người chơi!" });

            try
            {
                data.Id = 0;
                data.Timestamp = DateTime.UtcNow;

                // Lưu xuống Supabase (Gồm cả StudentId và IsServerChung do Unity gửi lên)
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

        // 2. API Lấy danh sách nhà (GET)
        [HttpGet("load-buildings")]
        public async Task<ActionResult<IEnumerable<BuildingDto>>> GetBuildings([FromQuery] string studentId, [FromQuery] bool isChung)
        {
            try
            {
                if (string.IsNullOrEmpty(studentId))
                {
                    return BadRequest(new { error = "Thiếu tham số studentId để nạp dữ liệu bản đồ!" });
                }

                List<BuildingDto> buildings;

                if (isChung)
                {
                    // 🌐 [SERVER CHUNG]: Chỉ lấy những nhà có IsServerChung == true (Của tất cả mọi người)
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == true)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - SERVER CHUNG] Đã nạp thành công {buildings.Count} căn nhà của toàn trường gửi cho Unity.");
                }
                else
                {
                    // 🏡 [SERVER RIÊNG]: Cách ly hoàn toàn, CHỈ lấy nhà có IsServerChung == false VÀ do CHÍNH studentId này xây
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == false && b.StudentId == studentId)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - MAP CÁ NHÂN] Đã nạp thành công {buildings.Count} căn nhà riêng của sinh viên '{studentId}'.");
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
*/
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Thêm thư viện này để dùng thuộc tính [Column]

namespace VibeCity_API.Data
{
    // --- Model chứa dữ liệu nhà ---
    [Table("Buildings")] // Đảm bảo chỉ định đích danh bảng
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

        // ✅ GIẢI PHÁP CHÍ MẠNG: Dùng thuộc tính Column để ép Entity Framework 
        // dịch thuộc tính này sang chữ thường "studentid" hoặc đúng định dạng cột Supabase quét được
        [Column("StudentId")]
        public string? StudentId { get; set; } = string.Empty;

        [Column("IsServerChung")]
        public bool IsServerChung { get; set; } = true;
    }
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

            if (string.IsNullOrEmpty(data.StudentId))
                return BadRequest(new { error = "Không thể xây nhà do thiếu tài khoản StudentId người chơi!" });

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

        // 2. API Lấy danh sách nhà (GET)
        [HttpGet("load-buildings")]
        public async Task<ActionResult<IEnumerable<BuildingDto>>> GetBuildings([FromQuery] string studentId, [FromQuery] bool isChung)
        {
            try
            {
                if (string.IsNullOrEmpty(studentId))
                {
                    return BadRequest(new { error = "Thiếu tham số studentId để nạp dữ liệu bản đồ!" });
                }

                List<BuildingDto> buildings;

                if (isChung)
                {
                    // 🌐 [SERVER CHUNG]
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == true)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - SERVER CHUNG] Đã nạp thành công {buildings.Count} căn nhà của toàn trường gửi cho Unity.");
                }
                else
                {
                    // 🏡 [SERVER RIÊNG]: So khớp chuẩn bằng Entity Framework qua khay Annotation
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == false && b.StudentId == studentId)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - MAP CÁ NHÂN] Đã nạp thành công {buildings.Count} căn nhà riêng của sinh viên '{studentId}'.");
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

        // 3. API Đăng nhập
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
                .FirstOrDefaultAsync(s => s.StudentId == loginInfo.StudentId && s.Password == loginInfo.Password);

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