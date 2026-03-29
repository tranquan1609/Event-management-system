using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> SelectEvent()
        {
            var events = await _context.Events
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();
            return View(events);
        }

        [HttpPost]
        public IActionResult SelectEvent(int eventId)
        {
            if (eventId > 0)
                return RedirectToAction(nameof(Index), new { eventId });
            TempData["Error"] = "Vui lòng chọn sự kiện.";
            return RedirectToAction(nameof(SelectEvent));
        }
        // Danh sách lịch trình theo sự kiện
        public async Task<IActionResult> Index(int eventId)
        {
            var eventItem = await _context.Events
                .Include(e => e.Schedule)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null) return NotFound();

            ViewBag.Event = eventItem;
            return View(eventItem.Schedule.OrderBy(s => s.StartTime).ToList());
        }

        // GET: Tạo mới lịch trình
        public IActionResult Create(int eventId)
        {
            ViewBag.EventId = eventId;
            return View();
        }

        // POST: Tạo mới lịch trình (chỉ nhập giờ, ghép ngày của sự kiện)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleItem schedule)
        {
            // Lấy ngày của sự kiện
            var eventItem = await _context.Events.FindAsync(schedule.EventId);
            if (eventItem == null)
            {
                TempData["Error"] = "Không tìm thấy sự kiện.";
                return RedirectToAction("SelectEvent");
            }

            // Ghép ngày của sự kiện với giờ/phút từ input
            // Nếu input là 01/01/0001 HH:mm thì chỉ lấy TimeOfDay
            schedule.StartTime = eventItem.StartTime.Date.Add(schedule.StartTime.TimeOfDay);
            schedule.EndTime = eventItem.StartTime.Date.Add(schedule.EndTime.TimeOfDay);

            if (ModelState.IsValid)
            {
                _context.ScheduleItems.Add(schedule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { eventId = schedule.EventId });
            }
            ViewBag.EventId = schedule.EventId;
            return View(schedule);
        }


        // GET: Sửa lịch trình
        public async Task<IActionResult> Edit(int id)
        {
            var schedule = await _context.ScheduleItems.FindAsync(id);
            if (schedule == null) return NotFound();
            return View(schedule);
        }

        // POST: Sửa lịch trình
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ScheduleItem schedule)
        {
            if (id != schedule.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(schedule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { eventId = schedule.EventId });
            }
            return View(schedule);
        }

        // Xóa lịch trình
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _context.ScheduleItems.FindAsync(id);
            if (schedule == null) return NotFound();
            var eventId = schedule.EventId;
            _context.ScheduleItems.Remove(schedule);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { eventId });
        }
    }
}
