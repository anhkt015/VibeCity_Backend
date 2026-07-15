using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeCity_API.Data
{
    // ==========================================
    // 1. SYSTEM MODEL: VÉ CHUYỂN MAP (OTP)
    // ==========================================
    [Table("teleport_tickets")]
    public class TeleportTicket
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("student_id")]
        public string StudentId { get; set; } = string.Empty;

        [Column("ticket_code")]
        public string TicketCode { get; set; } = string.Empty;

        [Column("target_map")]
        public string TargetMap { get; set; } = "map2"; // "map1" hoặc "map2"

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("consumed_at")]
        public DateTime? ConsumedAt { get; set; }
    }

    // ==========================================
    // 2. GAMEPLAY MODEL: KHO VẬT PHẨM KHÔNG TỌA ĐỘ
    // ==========================================
    [Table("player_inventory")]
    public class PlayerInventoryItem
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("student_id")]
        public string StudentId { get; set; } = string.Empty;

        [Required]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty; // "RAID_TICKET", "SECURITY_LOCK"

        [Column("quantity")]
        public int Quantity { get; set; } = 0;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==========================================
    // 3. GAMEPLAY MODEL: THỰC THỂ ĐỘNG VẬT (CHICKEN, BEAR)
    // ==========================================
    [Table("animal_instances")]
    public class AnimalInstance
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("animal_type")]
        public string AnimalType { get; set; } = "CHICKEN"; // "CHICKEN" hoặc "BEAR"

        [Required]
        [Column("owner_student_id")]
        public string OwnerStudentId { get; set; } = string.Empty;

        [Required]
        [Column("house_owner_student_id")]
        public string HouseOwnerStudentId { get; set; } = string.Empty;

        [Column("pos_x")]
        public double PosX { get; set; }

        [Column("pos_y")]
        public double PosY { get; set; }

        [Column("pos_z")]
        public double PosZ { get; set; }

        [Column("rot_y")]
        public double RotY { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==========================================
    // 4. GAMEPLAY MODEL: THU NHẬP THỤ ĐỘNG TỪ GÀ
    // ==========================================
    [Table("house_chicken_income")]
    public class HouseChickenIncome
    {
        [Key]
        [Column("house_owner_student_id")]
        public string HouseOwnerStudentId { get; set; } = string.Empty;

        [Column("pending_coin")]
        public int PendingCoin { get; set; } = 0;

        [Column("last_processed_hour")]
        public DateTime LastProcessedHour { get; set; } = DateTime.UtcNow;

        [Column("last_collected_at")]
        public DateTime? LastCollectedAt { get; set; }

        [Column("last_collector_student_id")]
        public string? LastCollectorStudentId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==========================================
    // 5. GAMEPLAY MODEL: Ổ KHÓA BẢO MẬT
    // ==========================================
    [Table("house_security_locks")]
    public class HouseSecurityLock
    {
        [Key]
        [Column("house_owner_student_id")]
        public string HouseOwnerStudentId { get; set; } = string.Empty;

        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [Required]
        [Column("question")]
        public string Question { get; set; } = string.Empty;

        [Required]
        [Column("answer_a")]
        public string AnswerA { get; set; } = string.Empty;

        [Required]
        [Column("answer_b")]
        public string AnswerB { get; set; } = string.Empty;

        [Column("correct_answer")]
        public int CorrectAnswer { get; set; } = 0; // 0 = A, 1 = B

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==========================================
    // 6. GAMEPLAY MODEL: PHIÊN ĐỘT KÍCH (ANTI-CHEAT)
    // ==========================================
    [Table("raid_sessions")]
    public class RaidSession
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("attacker_student_id")]
        public string AttackerStudentId { get; set; } = string.Empty;

        [Required]
        [Column("target_student_id")]
        public string TargetStudentId { get; set; } = string.Empty;

        [Required]
        [Column("status")]
        public string Status { get; set; } = "CREATED"; // CREATED, AWAITING_LOCK, ACTIVE, FAILED_LOCK, COMPLETED, EXPIRED, FAILED

        [Column("lock_required")]
        public bool LockRequired { get; set; } = false;

        [Column("lock_passed")]
        public bool LockPassed { get; set; } = false;

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("failed_return_at")]
        public DateTime? FailedReturnAt { get; set; }

        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }
    }

    // ==========================================
    // 7. SYSTEM MODEL: THƯỞNG CỔNG BÌNH MINH
    // ==========================================
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

    // ==========================================
    // DATABASE CONTEXT
    // ==========================================
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<BuildingDto> Buildings { get; set; }
        public DbSet<NpcDto> Npcs { get; set; }
        public DbSet<LessonDto> Lessons { get; set; }
        public DbSet<Student> Students { get; set; }

        // Đăng ký toàn bộ DbSet mới
        public DbSet<TeleportTicket> TeleportTickets { get; set; }
        public DbSet<PlayerInventoryItem> PlayerInventory { get; set; }
        public DbSet<AnimalInstance> AnimalInstances { get; set; }
        public DbSet<HouseChickenIncome> HouseChickenIncomes { get; set; }
        public DbSet<HouseSecurityLock> HouseSecurityLocks { get; set; }
        public DbSet<RaidSession> RaidSessions { get; set; }
        public DbSet<DawnGateReward> DawnGateRewards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NpcDto>().Property(n => n.NpcType).HasColumnName("npctype");

            // Cấu hình Unique constraints ngăn chặn trùng dữ liệu
            modelBuilder.Entity<DawnGateReward>()
                .HasIndex(x => new { x.StudentId, x.NightCycleId })
                .IsUnique();

            modelBuilder.Entity<PlayerInventoryItem>()
                .HasIndex(x => new { x.StudentId, x.ItemCode })
                .IsUnique();

            modelBuilder.Entity<TeleportTicket>()
                .HasIndex(x => x.TicketCode)
                .IsUnique();

            // Chuyển string sang text để tương thích hoàn hảo PostgreSQL (Supabase)
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