using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị danh sách thông báo
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách thông báo từ cơ sở dữ liệu
            var notifications = await _context.Notifications
                .Include(n => n.Event) // Bao gồm thông tin sự kiện liên quan
                .OrderByDescending(n => n.CreatedAt) // Sắp xếp theo thời gian tạo mới nhất
                .ToListAsync();

            return View(notifications);
        }

        // GET: Notification/Create
        public IActionResult Create()
        {
            // Lấy danh sách sự kiện để hiển thị trong dropdown
            ViewBag.Events = _context.Events.ToList();
            return View();
        }

        // POST: Notification/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Content,EventId")] Notification notification)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    notification.CreatedAt = DateTime.Now;

                    // Kiểm tra và load Event nếu có
                    if (notification.EventId.HasValue)
                    {
                        // Không cần gán Event trực tiếp, chỉ cần đảm bảo EventId được set
                        var eventExists = await _context.Events.AnyAsync(e => e.Id == notification.EventId.Value);
                        if (!eventExists)
                        {
                            ModelState.AddModelError("EventId", "Sự kiện không tồn tại");
                            ViewBag.Events = await _context.Events.ToListAsync();
                            return View(notification);
                        }
                    }

                    await _context.Notifications.AddAsync(notification);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu thông báo: " + ex.Message);
                }
            }

            // Nếu có lỗi, load lại danh sách sự kiện
            ViewBag.Events = await _context.Events.ToListAsync();
            return View(notification);
        }
    }
}