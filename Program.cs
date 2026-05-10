using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VibeCity_API.Data;

var builder = WebApplication.CreateBuilder(args);
/* builder.Services.AddDbContext<AppDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));*/
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- CẤU HÌNH QUAN TRỌNG ĐỂ THÔNG MẠNG ---
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Lắng nghe cổng 5057 từ tất cả các địa chỉ IP (bao gồm cả máy Nhật Anh)
    serverOptions.ListenAnyIP(5057);
});

// 1. Đăng ký dịch vụ Controller
builder.Services.AddControllers();

var app = builder.Build();

// 2. Cấu hình Middleware
// Tạm thời comment HttpsRedirection để test qua HTTP (cổng 5057) cho ổn định
// app.UseHttpsRedirection(); 

app.UseAuthorization();
app.MapControllers();

// 3. Chạy ứng dụng
app.Run();