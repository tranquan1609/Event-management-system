using DACSWEBSK.Models;
using DACSWEBSK.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminAttendeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminAttendeeController> _logger;

        public AdminAttendeeController(
            ApplicationDbContext context, 
            IEmailService emailService,
            ILogger<AdminAttendeeController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // Xóa người tham gia của các sự kiện đã kết thúc
        private async Task CleanupEndedEventAttendees()
        {
            var currentTime = DateTime.Now;
            var endedEventIds = await _context.Events
                .Where(e => e.EndTime < currentTime)
                .Select(e => e.Id)
                .ToListAsync();

            if (endedEventIds.Any())
            {
                var attendeesToDelete = await _context.Attendees
                    .Where(a => endedEventIds.Contains(a.EventId))
                    .ToListAsync();

                if (attendeesToDelete.Any())
                {
                    foreach (var attendee in attendeesToDelete)
                    {
                        attendee.IsDeleted = true;
                    }
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Danh sách attendee theo sự kiện
        public async Task<IActionResult> Index(int? eventId)
        {
            // Tự động xóa người tham gia của các sự kiện đã kết thúc
            await CleanupEndedEventAttendees();

            var attendees = _context.Attendees.Include(a => a.Event).AsQueryable();
            if (eventId.HasValue)
            {
                attendees = attendees.Where(a => a.EventId == eventId.Value);
            }
            ViewBag.Events = await _context.Events.ToListAsync();
            ViewBag.CurrentEventId = eventId;
            return View(await attendees.ToListAsync());
        }

        // GET: Edit attendee
        public async Task<IActionResult> Edit(int id)
        {
            var attendee = await _context.Attendees.Include(a => a.Event).FirstOrDefaultAsync(a => a.Id == id);
            if (attendee == null) return NotFound();
            
            // Prevent editing confirmed attendees
            if (attendee.TicketStatus == "Confirmed")
            {
                TempData["Error"] = "Không thể chỉnh sửa người tham gia đã được duyệt.";
                return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
            }
            
            return View(attendee);
        }

        // POST: Edit attendee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Email,Phone,TicketStatus,EventId")] Attendee attendee)
        {
            if (id != attendee.Id) return NotFound();

            // Check if attendee is already confirmed
            var existingAttendee = await _context.Attendees
                .Include(a => a.Event)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (existingAttendee?.TicketStatus == "Confirmed")
            {
                TempData["Error"] = "Không thể chỉnh sửa người tham gia đã được duyệt.";
                return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // If status is being changed to Confirmed, send approval email
                    if (attendee.TicketStatus == "Confirmed" && existingAttendee?.TicketStatus != "Confirmed")
                    {
                        try
                        {
                            var eventDetails = await _context.Events.FirstOrDefaultAsync(e => e.Id == attendee.EventId);
                            if (eventDetails != null)
                            {
                                await _emailService.SendEventApprovalEmailAsync(
                                    attendee.Email,
                                    attendee.FullName,
                                    eventDetails.Title,
                                    eventDetails.StartTime
                                );
                                _logger.LogInformation($"Approval email sent to {attendee.Email} for event {eventDetails.Title}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error sending approval email: {ex.Message}");
                            TempData["Error"] = "Đã xảy ra lỗi khi gửi email thông báo. Vui lòng kiểm tra cấu hình email.";
                            return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
                        }
                    }

                    _context.Update(attendee);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật trạng thái thành công!";
                    return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating attendee status: {ex.Message}");
                    TempData["Error"] = "Có lỗi xảy ra khi cập nhật trạng thái. Chi tiết: " + ex.Message;
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogError($"Invalid model state: {string.Join(", ", errors)}");
                TempData["Error"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
            }
            
            return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
        }
    }
}
