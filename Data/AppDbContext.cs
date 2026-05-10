using Microsoft.EntityFrameworkCore;

namespace VibeCity_API.Data // Thay bằng Namespace của ông
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Khai báo bảng Houses trong SQL Server
        public DbSet<BuildingDto> Buildings { get; set; }
        public DbSet<NpcDto> Npcs { get; set; }
        public DbSet<LessonDto> Lessons { get; set; }
        public DbSet<Student> Students { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tự động chuyển tất cả string từ nvarchar sang text để hợp với PostgreSQL
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(string))
                    {
                        property.SetColumnType("text");
                    }
                }
            }
        }

    }
}
