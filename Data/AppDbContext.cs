using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeCity_API.Data
{
    // 1. Model Vé dịch chuyển (Đã map cột id chữ thường để sửa lỗi PostgreSQL)
    [Table("teleport_tickets")]
    public class TeleportTicket
    {
        [Key]
        [Column("id")]
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

    // 2. Model Phần thưởng Cổng Bình Minh
    [Table("dawn_gate_rewards")]
    public sealed class DawnGateReward
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("student_id")]
        public string StudentId { get; set; } = string.Empty;

        [Required]
        [Column("night_cycle_id")]
        public string NightCycleId { get; set; } = string.Empty;

        [Column("reward_amount")]
        public int RewardAmount { get; set; } = 50;

        [Column("claimed_at")]
        public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
    }

    // 3. Lớp Context quản lý Database
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<BuildingDto> Buildings { get; set; }
        public DbSet<NpcDto> Npcs { get; set; }
        public DbSet<LessonDto> Lessons { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<TeleportTicket> TeleportTickets { get; set; }
        public DbSet<DawnGateReward> DawnGateRewards => Set<DawnGateReward>(); // Thêm DbSet phần thưởng

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NpcDto>().Property(n => n.NpcType).HasColumnName("npctype");

            // Ràng buộc Unique ngăn chặn nhận thưởng trùng lặp trong cùng một đêm
            modelBuilder.Entity<DawnGateReward>()
                .HasIndex(x => new { x.StudentId, x.NightCycleId })
                .IsUnique();

            // Tự động chuyển tất cả string sang text để tương thích hoàn hảo với PostgreSQL (Supabase)
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