using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeCity_API.Data
{
    [Table("teleport_tickets")]
    public class TeleportTicket
    {
        [Key]
        public int Id { get; set; }

        [Column("student_id")]
        public string StudentId { get; set; } = string.Empty;

        [Column("ticket_code")]
        public string TicketCode { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<BuildingDto> Buildings { get; set; }
        public DbSet<NpcDto> Npcs { get; set; }
        public DbSet<LessonDto> Lessons { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<TeleportTicket> TeleportTickets { get; set; } // Quản lý vé dịch chuyển Map 1 -> Map 2

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NpcDto>().Property(n => n.NpcType).HasColumnName("npctype");

            // Tự động chuyển tất cả string từ nvarchar sang text để tương thích hoàn hảo với PostgreSQL (Supabase)
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