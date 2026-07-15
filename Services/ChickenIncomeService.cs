using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using VibeCity_API.Data;

namespace VibeCity_API.Services
{
    public interface IChickenIncomeService
    {
        Task<HouseChickenIncome> ProcessIncomeAsync(string houseOwnerStudentId, CancellationToken cancellationToken);
    }

    public class ChickenIncomeService : IChickenIncomeService
    {
        private readonly AppDbContext _context;

        public ChickenIncomeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HouseChickenIncome> ProcessIncomeAsync(string houseOwnerStudentId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            // Làm tròn thời gian hiện tại xuống đầu giờ (Ví dụ: 14:35 -> 14:00)
            var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

            var income = await _context.HouseChickenIncomes
                .FirstOrDefaultAsync(x => x.HouseOwnerStudentId == houseOwnerStudentId, cancellationToken);

            // Đếm số gà đang hoạt động tại căn nhà này từ DB thực tế
            int chickenCount = await _context.AnimalInstances.CountAsync(x =>
                x.HouseOwnerStudentId == houseOwnerStudentId &&
                x.AnimalType == "CHICKEN" &&
                x.IsActive, cancellationToken);

            if (income == null)
            {
                // Chưa từng có lịch sử tích lũy, tạo mới bắt đầu từ giờ hiện tại
                income = new HouseChickenIncome
                {
                    HouseOwnerStudentId = houseOwnerStudentId,
                    PendingCoin = 0,
                    LastProcessedHour = currentHour,
                    UpdatedAt = now
                };
                _context.HouseChickenIncomes.Add(income);
                await _context.SaveChangesAsync(cancellationToken);
                return income;
            }

            // Tính số giờ đầy đủ thực tế đã trôi qua kể từ lần xử lý trước
            double elapsedHoursDouble = (currentHour - income.LastProcessedHour).TotalHours;
            int elapsedHours = (int)Math.Floor(elapsedHoursDouble);

            if (elapsedHours > 0)
            {
                // Công thức: Giờ trôi qua * Số gà * 10 VibeCoin
                int generatedCoin = elapsedHours * chickenCount * 10;
                income.PendingCoin += generatedCoin;
                income.LastProcessedHour = income.LastProcessedHour.AddHours(elapsedHours);
                income.UpdatedAt = now;

                await _context.SaveChangesAsync(cancellationToken);
            }

            return income;
        }
    }
}