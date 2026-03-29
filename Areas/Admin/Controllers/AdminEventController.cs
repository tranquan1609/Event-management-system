using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminEventController : Controller
    {
        private readonly IEventRepository _eventRepository;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminEventController(IEventRepository eventRepository, ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _eventRepository = eventRepository;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Event
        public async Task<IActionResult> Index()
        {
            // Cập nhật trạng thái sự kiện trước khi hiển thị
            var now = DateTime.Now;
            var events = await _context.Events
                .Include(e => e.Location)
                .Include(e => e.Attendees)
                .OrderByDescending(e => e.Id) // Sắp xếp ID từ cao xuống thấp
                .ToListAsync();

            foreach (var evt in events)
            {
                // Cập nhật trạng thái dựa trên thời gian
                if (evt.Status != "Cancelled")
                {
                    if (now > evt.EndTime && evt.Status != "Completed")
                    {
                        evt.Status = "Completed";
                    }
                    else if (now >= evt.StartTime && now <= evt.EndTime && evt.Status != "Ongoing")
                    {
                        evt.Status = "Ongoing";
                    }
                    else if (now < evt.StartTime && evt.Status != "Upcoming")
                    {
                        evt.Status = "Upcoming";
                    }
                }
            }

            await _context.SaveChangesAsync();
            return View(events);
        }

        // GET: Admin/Event/Create
        public IActionResult Create()
        {
            ViewBag.Locations = new SelectList(_context.Locations, "Id", "Name");
            return View();
        }

        // POST: Admin/Event/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event model, IFormFile imageFile)
        {
            if (model.LocationId <= 0)
            {
                ModelState.AddModelError("LocationId", "Vui lòng chọn địa điểm cho sự kiện");
            }

            // Validation: Kiểm tra thời gian không được trong quá khứ
            var now = DateTime.Now;
            if (model.StartTime < now)
            {
                ModelState.AddModelError("StartTime", "Thời gian bắt đầu không được trong quá khứ!");
            }

            if (model.EndTime < now)
            {
                ModelState.AddModelError("EndTime", "Thời gian kết thúc không được trong quá khứ!");
            }

            // Validation: EndTime phải sau StartTime
            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "Thời gian kết thúc phải sau thời gian bắt đầu!");
            }

            // Đảm bảo Status luôn là "Upcoming" khi tạo mới
            model.Status = "Upcoming";

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    // Tạo tên file duy nhất
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    
                    // Tạo đường dẫn đến thư mục lưu ảnh
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "events");
                    
                    // Tạo thư mục nếu chưa tồn tại
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    
                    // Lưu file
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    // Cập nhật đường dẫn ảnh cho sự kiện
                    model.ImageUrl = "/images/events/" + uniqueFileName;
                }

                await _eventRepository.AddAsync(model);
                return RedirectToAction(nameof(Index));
            }

            // Re-populate dropdown if validation fails
            ViewBag.Locations = new SelectList(_context.Locations, "Id", "Name");
            return View(model);
        }

        // GET: Admin/Event/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var evt = await _eventRepository.GetByIdAsync(id);
            if (evt == null) return NotFound();

            ViewBag.Locations = new SelectList(_context.Locations, "Id", "Name", evt.LocationId);
            return View(evt);
        }

        // POST: Admin/Event/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Event model)
        {
            if (id != model.Id) return NotFound();

            // Lấy thông tin sự kiện gốc để so sánh
            var originalEvent = await _eventRepository.GetByIdAsync(id);
            if (originalEvent == null) return NotFound();

            var now = DateTime.Now;

            // Validation: Kiểm tra thời gian mới không được trong quá khứ
            // Cho phép giữ nguyên thời gian cũ nếu đang chỉnh sửa sự kiện đã qua
            if (model.StartTime < now && model.StartTime < originalEvent.StartTime)
            {
                ModelState.AddModelError("StartTime", "Thời gian bắt đầu không được trong quá khứ!");
            }

            if (model.EndTime < now && model.EndTime < originalEvent.EndTime)
            {
                ModelState.AddModelError("EndTime", "Thời gian kết thúc không được trong quá khứ!");
            }

            // Validation: EndTime phải sau StartTime
            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "Thời gian kết thúc phải sau thời gian bắt đầu!");
            }

            if (ModelState.IsValid)
            {
                // Giữ nguyên Status từ sự kiện gốc (Status sẽ được tự động cập nhật bởi background service)
                model.Status = originalEvent.Status;
                
                await _eventRepository.UpdateAsync(model);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Locations = new SelectList(_context.Locations, "Id", "Name", model.LocationId);
            return View(model);
        }

        // GET: Admin/Event/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var evt = await _context.Events
                .Include(e => e.Location) // Include location details
                .Include(e => e.Assignments)
                    .ThenInclude(a => a.Submissions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evt == null) return NotFound();
            return View(evt);
        }

        // GET: Admin/Event/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var evt = await _context.Events
                .Include(e => e.Location)
                .Include(e => e.Assignments)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            if (evt == null) return NotFound();

            // Kiểm tra số lượng bài thu hoạch
            var assignmentCount = evt.Assignments?.Count ?? 0;
            ViewBag.AssignmentCount = assignmentCount;
            if (assignmentCount > 0)
            {
                ViewBag.Assignments = await _context.EventAssignments
                    .Where(a => a.EventId == id)
                    .Select(a => a.Title)
                    .ToListAsync();
            }

            return View(evt);
        }

        // POST: Admin/Event/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var evt = await _context.Events
                .Include(e => e.Notifications)
                .Include(e => e.Attendees)
                .Include(e => e.Schedule)
                .Include(e => e.Questions)
                .Include(e => e.Videos)
                .Include(e => e.Assignments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evt == null)
            {
                return NotFound();
            }

            // Kiểm tra xem sự kiện có bài thu hoạch không
            var assignmentCount = await _context.EventAssignments
                .Where(a => a.EventId == id)
                .CountAsync();

            if (assignmentCount > 0)
            {
                TempData["Error"] = $"Không thể xóa sự kiện này vì còn {assignmentCount} bài thu hoạch. Vui lòng xóa các bài thu hoạch trước khi xóa sự kiện.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                // Xóa các thông báo liên quan
                if (evt.Notifications != null && evt.Notifications.Any())
                {
                    _context.Notifications.RemoveRange(evt.Notifications);
                }

                // Xóa các người tham gia
                if (evt.Attendees != null && evt.Attendees.Any())
                {
                    _context.Attendees.RemoveRange(evt.Attendees);
                }

                // Xóa lịch trình
                if (evt.Schedule != null && evt.Schedule.Any())
                {
                    _context.ScheduleItems.RemoveRange(evt.Schedule);
                }

                // Xóa câu hỏi
                if (evt.Questions != null && evt.Questions.Any())
                {
                    _context.Questions.RemoveRange(evt.Questions);
                }

                // Xóa videos
                if (evt.Videos != null && evt.Videos.Any())
                {
                    _context.Videos.RemoveRange(evt.Videos);
                }

                // Xóa sự kiện
                _context.Events.Remove(evt);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã xóa sự kiện thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra khi xóa sự kiện: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
