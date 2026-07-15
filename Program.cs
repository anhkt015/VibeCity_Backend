using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VibeCity_API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using VibeCity_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình CORS cho phép itch.io truy cập an toàn
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 1. Cấu hình Database PostgreSQL (Supabase)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Đăng ký dịch vụ tạo Token tập trung
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// 3. Bật Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. Cấu hình Kestrel Port
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5057);
});

builder.Services.AddControllers();

// 5. Đồng bộ cấu hình Middleware Xác thực JWT
string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key trong appsettings.json hoặc Environment Variables.");
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "VibeCityBackend";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "VibeCityUnity";

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key phải dài tối thiểu 32 byte!");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        NameClaimType = ClaimTypes.Name
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// --- CẤU HÌNH MIDDLEWARE ---

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeCity API V1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication(); // PHẢI CHẠY TRƯỚC AUTHORIZATION!
app.UseAuthorization();

app.MapControllers();

app.Run();