using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DACSWEBSK.Controllers
{
    public class EventController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EventController> _logger;

        public EventController(ApplicationDbContext context, ILogger<EventController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Hiển thị danh sách sự kiện với filter theo loại
        public async Task<IActionResult> Index(string status, string searchString)
        {
            var query = _context.Events
                .Include(e => e.Location)
                .Include(e => e.Attendees)
                .AsQueryable();

            // Lọc theo trạng thái nếu có chọn
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(e => e.Status == status);
            }

            // Tìm kiếm linh hoạt theo tên, mô tả, địa điểm
            if (!string.IsNullOrEmpty(searchString))
            {
                var searchTerms = searchString.Trim().ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                query = query.Where(e =>
                    searchTerms.All(term =>
                        e.Title.ToLower().Contains(term) ||
                        (e.Description != null && e.Description.ToLower().Contains(term)) ||
                        (e.Location != null && e.Location.Name.ToLower().Contains(term))
                    )
                );
            }

            var events = await query.ToListAsync();

            // Sắp xếp theo thứ tự ưu tiên: Upcoming > Ongoing > Completed > Cancelled
            var now = DateTime.Now;
            events = events.OrderBy(e =>
            {
                // Xác định trạng thái thực tế dựa trên thời gian
                var actualStatus = e.Status == "Cancelled" ? "Cancelled" :
                    (now < e.StartTime ? "Upcoming" :
                     now <= e.EndTime ? "Ongoing" : "Completed");

                // Thứ tự ưu tiên: Upcoming = 1, Ongoing = 2, Completed = 3, Cancelled = 4
                return actualStatus switch
                {
                    "Upcoming" => 1,
                    "Ongoing" => 2,
                    "Completed" => 3,
                    "Cancelled" => 4,
                    _ => 5
                };
            })
            .ThenBy(e => e.StartTime) // Sau đó sắp xếp theo thời gian bắt đầu
            .ToList();

            // Truyền trạng thái filter và từ khóa tìm kiếm cho view
            ViewBag.CurrentStatus = status ?? "All";
            ViewBag.CurrentSearch = searchString;

            // Truyền model trực tiếp
            return View(events);
        }

        // Hiển thị chi tiết sự kiện
        public IActionResult Details(int id)
        {
            var evt = _context.Events
                .Include(e => e.Schedule)
                .Include(e => e.Attendees)
                .Include(e => e.Location)
                .Include(e => e.Videos)
                .Include(e => e.Questions)
                .Include(e => e.Assignments)
                .FirstOrDefault(e => e.Id == id);

            if (evt == null)
                return NotFound();

            return View(evt);
        }

        // GET: Event/Register/5
        public async Task<IActionResult> Register(int id)
        {
            var evt = await _context.Events
                .Include(e => e.Location)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evt == null)
                return NotFound();

            if (evt.Status != "Upcoming")
            {
                TempData["Error"] = "Sự kiện này không trong thời gian đăng ký.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Event = evt;
            return View(new Attendee { EventId = id });
        }

        // GET: Event/RegisterAttendee/5
        public async Task<IActionResult> RegisterAttendee(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evt = await _context.Events
                .Include(e => e.Location)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evt == null)
            {
                return NotFound();
            }

            if (evt.Status == "Cancelled" || DateTime.Now > evt.EndTime)
            {
                TempData["RegisterMessage"] = "Sự kiện này đã kết thúc hoặc bị hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Event = evt;
            return View(new Attendee { EventId = id.Value });
        }

        // Xử lý đăng ký tham gia sự kiện
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterAttendee([Bind("FullName,Email,Phone,EventId")] Attendee attendee)
        {
            var evt = await _context.Events.FindAsync(attendee.EventId);
            
            if (!ModelState.IsValid)
            {
                ViewBag.Event = evt;
                return View(attendee);
            }

            if (evt == null)
            {
                TempData["Error"] = "Không tìm thấy sự kiện.";
                return RedirectToAction(nameof(Index));
            }

            if (evt.Status == "Cancelled" || DateTime.Now > evt.EndTime)
            {
                ModelState.AddModelError("", "Sự kiện này đã kết thúc hoặc bị hủy.");
                ViewBag.Event = evt;
                return View(attendee);
            }

            var exists = await _context.Attendees
                .AnyAsync(a => a.EventId == attendee.EventId && a.Email == attendee.Email);

            if (exists)
            {
                ModelState.AddModelError("Email", "Email này đã được đăng ký cho sự kiện!");
                ViewBag.Event = evt;
                return View(attendee);
            }

            try
            {
                attendee.TicketStatus = "Pending";
                _context.Attendees.Add(attendee);

                // Cộng điểm cho user nếu có tài khoản
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == attendee.Email);
                if (user != null)
                {
                    user.Points += 10;
                    _context.Users.Update(user);
                }

                await _context.SaveChangesAsync();
                TempData["RegisterMessage"] = "Đăng ký thành công! Vui lòng chờ admin duyệt vé.";
                return RedirectToAction(nameof(Details), new { id = attendee.EventId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi đăng ký. Vui lòng thử lại sau.");
                _logger.LogError(ex, "Error registering attendee");
                ViewBag.Event = evt;
                return View(attendee);
            }
        }

        [HttpPost]
        public IActionResult AskQuestion(int eventId, string content, string email)
        {
            var attendee = _context.Attendees
                .FirstOrDefault(a => a.EventId == eventId && a.Email == email && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["QuestionMessage"] = "Bạn cần đăng ký và được duyệt mới có thể đặt câu hỏi.";
                return RedirectToAction("Details", new { id = eventId });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["QuestionMessage"] = "Nội dung câu hỏi không được để trống.";
                return RedirectToAction("Details", new { id = eventId });
            }

            var question = new Question
            {
                Content = content,
                CreatedAt = DateTime.Now,
                AskedBy = attendee.FullName,
                EventId = eventId
            };
            _context.Questions.Add(question);

            // Cộng 50 điểm cho user nếu có tài khoản với email này
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                user.Points += 50;
                _context.Users.Update(user);
            }

            _context.SaveChanges();
            TempData["QuestionMessage"] = "Gửi câu hỏi thành công!";
            return RedirectToAction("Details", new { id = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyToQuestion(int questionId, string replyContent)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để trả lời câu hỏi.";
                // Redirect to login or appropriate page
                return RedirectToAction("Details", "Event", new { id = (await _context.Questions.FindAsync(questionId))?.EventId });
            }

            var question = await _context.Questions.FindAsync(questionId);
            if (question == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy câu hỏi.";
                return RedirectToAction("Index"); // Or a specific error page
            }

            if (string.IsNullOrWhiteSpace(replyContent))
            {
                TempData["ErrorMessage"] = "Nội dung trả lời không được để trống.";
                return RedirectToAction("Details", new { id = question.EventId });
            }

            question.Reply = replyContent;
            question.RepliedAt = DateTime.Now;
            // Lấy tên người dùng đã đăng nhập
            question.RepliedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity.Name; 

            _context.Update(question);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã gửi câu trả lời thành công!";
            return RedirectToAction("Details", new { id = question.EventId });
        }

        // GET: Event/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .Include(e => e.Location)
                .Include(e => e.Attendees)
                .Include(e => e.Videos)
                .Include(e => e.Questions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: Event/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var @event = await _context.Events
                    .Include(e => e.Attendees)
                    .Include(e => e.Videos)
                    .Include(e => e.Questions)
                    .Include(e => e.Schedule)
                    .Include(e => e.Notifications)
                    .Include(e => e.Assignments)
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (@event == null)
                {
                    return NotFound();
                }

                // Kiểm tra điều kiện trước khi xóa
                if (@event.Status == "InProgress")
                {
                    TempData["Error"] = "Không thể xóa sự kiện đang diễn ra";
                    return RedirectToAction(nameof(Index));
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

                _logger.LogInformation($"Bắt đầu xóa sự kiện {id} với {@event.Attendees.Count} người tham dự");

                // Xóa VideoProgresses trước
                var videoProgresses = await _context.VideoProgresses
                    .Where(vp => vp.Video.EventId == id)
                    .ToListAsync();
                if (videoProgresses.Any())
                {
                    _context.VideoProgresses.RemoveRange(videoProgresses);
                    _logger.LogInformation($"Đã xóa {videoProgresses.Count} video progress");
                }

                // Xóa Certificates
                var certificates = await _context.Certificates
                    .Where(c => c.EventId == id)
                    .ToListAsync();
                if (certificates.Any())
                {
                    _context.Certificates.RemoveRange(certificates);
                    _logger.LogInformation($"Đã xóa {certificates.Count} certificates");
                }

                // Xóa Questions
                if (@event.Questions != null && @event.Questions.Any())
                {
                    _context.Questions.RemoveRange(@event.Questions);
                    _logger.LogInformation($"Đã xóa {@event.Questions.Count} câu hỏi");
                }

                // Xóa Videos
                if (@event.Videos != null && @event.Videos.Any())
                {
                    _context.Videos.RemoveRange(@event.Videos);
                    _logger.LogInformation($"Đã xóa {@event.Videos.Count} videos");
                }

                // Xóa Schedule Items
                if (@event.Schedule != null && @event.Schedule.Any())
                {
                    _context.ScheduleItems.RemoveRange(@event.Schedule);
                    _logger.LogInformation($"Đã xóa {@event.Schedule.Count} lịch trình");
                }

                // Xóa Notifications
                if (@event.Notifications != null && @event.Notifications.Any())
                {
                    _context.Notifications.RemoveRange(@event.Notifications);
                    _logger.LogInformation($"Đã xóa {@event.Notifications.Count} thông báo");
                }

                // Xóa Attendees
                if (@event.Attendees != null && @event.Attendees.Any())
                {
                    _context.Attendees.RemoveRange(@event.Attendees);
                    _logger.LogInformation($"Đã xóa {@event.Attendees.Count} người tham dự");
                }

                // Cuối cùng xóa Event
                _context.Events.Remove(@event);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Đã xóa sự kiện thành công";
                _logger.LogInformation($"Đã xóa thành công sự kiện {id}");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Lỗi khi xóa sự kiện {id}: {ex.Message}");
                TempData["Error"] = "Có lỗi xảy ra khi xóa sự kiện. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}