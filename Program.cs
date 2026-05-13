using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VibeCity_API.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database (Npgsql cho Supabase)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Bật Swagger Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Cấu hình Kestrel (Port 5057 cho Render)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5057);
});

builder.Services.AddControllers();

var app = builder.Build();

// --- CẤU HÌNH MIDDLEWARE (Thứ tự rất quan trọng) ---

// Bật CORS
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// Bật Swagger cho cả Dev và Production (Render)
// Phải nằm TRƯỚC MapControllers và Run
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeCity API V1");
    c.RoutePrefix = "swagger";
});

// Tạm thời tắt HttpsRedirection nếu chạy trên Render port 5057 để tránh lỗi redirect vòng lặp
// app.UseHttpsRedirection(); 

app.UseAuthorization();
app.MapControllers();

// CHỈ GỌI DUY NHẤT 1 LẦN Ở CUỐI FILE
app.Run();