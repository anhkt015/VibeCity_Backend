/*
using BCrypt.Net;// Thêm thư viện này để dùng thuộc tính [Column]
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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
        private readonly IConfiguration _configuration;

        public BuildingController(
        AppDbContext context,
        IConfiguration configuration)
            {
                _context = context;
                _configuration = configuration;
            }
        private string GenerateJwtToken(Student student)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, student.StudentId),
                new Claim("FullName", student.FullName),
                new Claim("Major", student.Major)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(

                issuer: _configuration["Jwt:Issuer"],

                audience: _configuration["Jwt:Audience"],

                claims: claims,

                expires: DateTime.UtcNow.AddHours(2),

                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
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
             .FirstOrDefaultAsync(s => s.StudentId == loginInfo.StudentId);

            if (student == null)
                return Unauthorized(new { error = "Sai Username hoặc mật khẩu!" });

            bool ok = BCrypt.Net.BCrypt.Verify(
                loginInfo.Password,
                student.Password);

            if (!ok)
                return Unauthorized(new { error = "Sai Username hoặc mật khẩu!" });
            if (student == null)
                return Unauthorized(new { error = "Sai Username hoặc mật khẩu!" });

            Console.WriteLine($"✅ Sinh viên {student.FullName} đã đăng nhập thành công.");

            string token = GenerateJwtToken(student);

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                accessToken = token,

                studentId = student.StudentId,
                fullName = student.FullName,
                major = student.Major,
                gpa = student.Gpa,
                unlockedSkills = student.UnlockedSkills
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
                newUser.Password =
                BCrypt.Net.BCrypt.HashPassword(newUser.Password);

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
        // 🟢 API CẬP NHẬT KỸ NĂNG MỚI ĐƯỢC MỞ KHÓA LÊN SUPABASE
        // Tạo class nhận JSON này ở ngoài hoặc trong Controller
        public class SkillUpdateRequest
        {
            public string StudentId { get; set; } = string.Empty;
            public int SkillId { get; set; }
        }

        // Sửa lại API cập nhật kỹ năng
        [HttpPost("update-skills")]
        public async Task<IActionResult> UpdateUnlockedSkills([FromBody] SkillUpdateRequest request) // Đổi sang [FromBody]
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentId))
                    return BadRequest(new { error = "Thiếu StudentId!" });

                // Tìm sinh viên trong database
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
                if (student == null)
                    return NotFound(new { error = "Không tìm thấy sinh viên!" });

                // Xử lý nối chuỗi với request.SkillId
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
    }
}*/
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

        // 1. API Xây nhà (POST) - Yêu cầu xác thực JWT
        [Authorize]
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

        // 2. API Lấy danh sách nhà (GET) - Yêu cầu xác thực JWT
        [Authorize]
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
                    buildings = await _context.Buildings
                        .Where(b => b.IsServerChung == true)
                        .OrderBy(b => b.Id)
                        .ToListAsync();

                    Console.WriteLine($"🔍 [GET - SERVER CHUNG] Đã nạp thành công {buildings.Count} căn nhà của toàn trường gửi cho Unity.");
                }
                else
                {
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

        // API Xóa công trình - Yêu cầu xác thực JWT
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

        // 3. API Đăng nhập (Sinh Token JWT)
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

            // ─── TIẾN HÀNH KHỞI TẠO JWT TOKEN CHO SINH VIÊN VỪA ĐĂNG NHẬP THÀNH CÔNG ───
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = Environment.GetEnvironmentVariable("Jwt_Key") ?? _configuration["Jwt:Key"] ?? "Key_Mac_Dinh_Sieu_Bao_Mat_VibeCity_2026";
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("StudentId", student.StudentId),
                    new Claim("FullName", student.FullName),
                    new Claim("Major", student.Major)
                }),
                Expires = DateTime.UtcNow.AddDays(7), // Thời hạn hết hạn token (Ví dụ: 7 ngày)
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
                token = tokenString, // Trả token JWT về cho Client/Unity lưu trữ
                studentId = student.StudentId,
                fullName = student.FullName,
                major = student.Major,
                gpa = student.Gpa,
                unlockedSkills = student.UnlockedSkills
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
                newUser.Password = BCrypt.Net.BCrypt.HashPassword(newUser.Password);

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

        // API Cập nhật kỹ năng - Yêu cầu xác thực JWT
        [Authorize]
        [HttpPost("update-skills")]
        public async Task<IActionResult> UpdateUnlockedSkills([FromBody] SkillUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentId))
                    return BadRequest(new { error = "Thiếu StudentId!" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
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
    }

    public class SkillUpdateRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public int SkillId { get; set; }
    }
}