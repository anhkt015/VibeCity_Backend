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

// --- THÊM DÒNG NÀY ĐỂ BẬT SWAGGER ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ------------------------------------

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5057);
});

builder.Services.AddControllers();

var app = builder.Build();

// --- BẬT SWAGGER TRÊN PRODUCTION (RENDER) ---
// Đưa ra ngoài if (app.Environment.IsDevelopment()) để Render cũng xem được
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeCity API V1");
    c.RoutePrefix = "swagger"; // Đường dẫn sẽ là /swagger
});
// --------------------------------------------

app.UseAuthorization();
app.MapControllers();

app.Run();