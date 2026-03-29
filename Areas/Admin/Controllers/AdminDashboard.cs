using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminDashboard : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboard(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var upcomingEventsCount = await _context.Events
                .CountAsync(e => e.Status == "Upcoming");

            var totalAttendeesCount = await _context.Attendees.CountAsync();

            var remainingGiftsCount = await _context.Gifts
                .Where(g => g.IsRandomCode == false)
                .CountAsync();

            var newNotificationsCount = await _context.Notifications
                .CountAsync(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-7));

            // Thống kê sự kiện theo tháng
            var currentYear = DateTime.Now.Year;
            var monthlyEventCounts = await _context.Events
                .Where(e => e.StartTime.Year == currentYear)
                .GroupBy(e => e.StartTime.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();

            int[] monthlyCounts = new int[12];
            foreach (var item in monthlyEventCounts)
            {
                monthlyCounts[item.Month - 1] = item.Count;
            }

            // Thống kê sự kiện theo ngày trong tháng hiện tại
            var currentMonth = DateTime.Now.Month;
            var dailyEventCounts = await _context.Events
                .Where(e => e.StartTime.Year == currentYear && e.StartTime.Month == currentMonth)
                .GroupBy(e => e.StartTime.Day)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
            int[] dailyCounts = new int[daysInMonth];
            foreach (var item in dailyEventCounts)
            {
                dailyCounts[item.Day - 1] = item.Count;
            }

            ViewBag.MonthlyEventCounts = monthlyCounts;
            ViewBag.DailyEventCounts = dailyCounts;
            ViewBag.UpcomingEventsCount = upcomingEventsCount;
            ViewBag.TotalAttendeesCount = totalAttendeesCount;
            ViewBag.RemainingGiftsCount = remainingGiftsCount;
            ViewBag.NewNotificationsCount = newNotificationsCount;

            return View();
        }
    }
}
