using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Models;
using DACSWEBSK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class AdminCertificateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICertificateService _certificateService;

        public AdminCertificateController(
            ApplicationDbContext context,
            ICertificateService certificateService)
        {
            _context = context;
            _certificateService = certificateService;
        }

        // GET: Admin/Certificate
        public async Task<IActionResult> Index(int? eventId)
        {
            var events = await _context.Events
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();
            ViewBag.Events = events;
            ViewBag.CurrentEventId = eventId;

            if (eventId.HasValue)
            {
                var certificates = await _certificateService.GetCertificatesByEventAsync(eventId.Value);
                return View(certificates);
            }

            return View(new List<Certificate>());
        }

        // POST: Admin/Certificate/GenerateForEvent/5
        [HttpPost]
        public async Task<IActionResult> GenerateForEvent(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Attendees)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (@event == null)
            {
                TempData["Error"] = "Không tìm thấy sự kiện.";
                return RedirectToAction(nameof(Index));
            }

            int successCount = 0;
            foreach (var attendee in @event.Attendees.Where(a => a.TicketStatus == "Confirmed"))
            {
                // Check if certificate already exists
                var existingCert = await _context.Certificates
                    .FirstOrDefaultAsync(c => c.AttendeeId == attendee.Id && c.EventId == eventId);

                if (existingCert == null)
                {
                    var certificate = await _certificateService.GenerateCertificateAsync(attendee, @event);
                    if (await _certificateService.SendCertificateEmailAsync(certificate))
                    {
                        successCount++;
                    }
                }
            }

            TempData["Success"] = $"Đã tạo và gửi {successCount} chứng chỉ thành công.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        // POST: Admin/Certificate/GenerateForAttendee/5
        [HttpPost]
        public async Task<IActionResult> GenerateForAttendee(int attendeeId)
        {
            var attendee = await _context.Attendees
                .Include(a => a.Event)
                .FirstOrDefaultAsync(a => a.Id == attendeeId);

            if (attendee == null || attendee.TicketStatus != "Confirmed")
            {
                TempData["Error"] = "Không tìm thấy người tham gia hoặc chưa được xác nhận.";
                return RedirectToAction(nameof(Index));
            }

            // Check if certificate already exists
            var existingCert = await _context.Certificates
                .FirstOrDefaultAsync(c => c.AttendeeId == attendeeId && c.EventId == attendee.EventId);

            if (existingCert != null)
            {
                TempData["Error"] = "Chứng chỉ đã được tạo cho người này.";
                return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
            }

            var certificate = await _certificateService.GenerateCertificateAsync(attendee, attendee.Event);
            if (await _certificateService.SendCertificateEmailAsync(certificate))
            {
                TempData["Success"] = "Đã tạo và gửi chứng chỉ thành công.";
            }
            else
            {
                TempData["Error"] = "Đã tạo chứng chỉ nhưng không gửi được email.";
            }

            return RedirectToAction(nameof(Index), new { eventId = attendee.EventId });
        }

        // GET: Admin/Certificate/Verify
        public IActionResult Verify()
        {
            return View();
        }

        // POST: Admin/Certificate/Verify
        [HttpPost]
        public async Task<IActionResult> Verify(string certificateNumber)
        {
            if (string.IsNullOrEmpty(certificateNumber))
            {
                TempData["Error"] = "Vui lòng nhập mã chứng chỉ.";
                return View();
            }

            var certificate = await _context.Certificates
                .Include(c => c.Attendee)
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber);

            if (certificate == null)
            {
                TempData["Error"] = "Không tìm thấy chứng chỉ với mã này.";
                return View();
            }

            return View("VerifyResult", certificate);
        }
    }
} 