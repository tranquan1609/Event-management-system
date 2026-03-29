using DACSWEBSK.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminEvidenceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AdminEvidenceController> _logger;

        public AdminEvidenceController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<AdminEvidenceController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // GET: Admin/AdminEvidence
        public async Task<IActionResult> Index(int? eventId, string? status)
        {
            var query = _context.OfflineAttendanceEvidences
                .Include(e => e.Event)
                .Include(e => e.Attendee)
                .AsQueryable();

            if (eventId.HasValue)
            {
                query = query.Where(e => e.EventId == eventId.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(e => e.Status == status);
            }

            ViewBag.Events = await _context.Events.OrderBy(e => e.StartTime).ToListAsync();
            ViewBag.CurrentEventId = eventId;
            ViewBag.CurrentStatus = status ?? "All";

            var evidences = await query.OrderByDescending(e => e.UploadedAt).ToListAsync();
            return View(evidences);
        }

        // GET: Admin/AdminEvidence/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evidence = await _context.OfflineAttendanceEvidences
                .Include(e => e.Event)
                .Include(e => e.Attendee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (evidence == null)
            {
                return NotFound();
            }

            return View(evidence);
        }

        // GET: Admin/AdminEvidence/DownloadFile/5
        public async Task<IActionResult> DownloadFile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evidence = await _context.OfflineAttendanceEvidences.FindAsync(id);
            if (evidence == null || string.IsNullOrEmpty(evidence.FilePath))
            {
                return NotFound();
            }

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, evidence.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/octet-stream", evidence.FileName);
        }

        // POST: Admin/AdminEvidence/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var evidence = await _context.OfflineAttendanceEvidences
                .Include(e => e.Attendee)
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evidence == null)
            {
                return NotFound();
            }

            if (evidence.Status == "Approved")
            {
                TempData["Info"] = "Minh chứng này đã được duyệt rồi!";
                return RedirectToAction(nameof(Details), new { id });
            }

            evidence.Status = "Approved";
            evidence.ProcessedAt = DateTime.Now;
            evidence.ProcessedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";
            evidence.RejectionReason = null;

            _context.Update(evidence);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã duyệt minh chứng thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Admin/AdminEvidence/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? rejectionReason)
        {
            var evidence = await _context.OfflineAttendanceEvidences
                .Include(e => e.Attendee)
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evidence == null)
            {
                return NotFound();
            }

            if (evidence.Status == "Rejected")
            {
                TempData["Info"] = "Minh chứng này đã bị từ chối rồi!";
                return RedirectToAction(nameof(Details), new { id });
            }

            evidence.Status = "Rejected";
            evidence.ProcessedAt = DateTime.Now;
            evidence.ProcessedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";
            evidence.RejectionReason = rejectionReason;

            _context.Update(evidence);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã từ chối minh chứng!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Admin/AdminEvidence/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var evidence = await _context.OfflineAttendanceEvidences.FindAsync(id);
            if (evidence == null)
            {
                return NotFound();
            }

            // Xóa file vật lý
            if (!string.IsNullOrEmpty(evidence.FilePath))
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, evidence.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not delete file {filePath}: {ex.Message}");
                    }
                }
            }

            _context.OfflineAttendanceEvidences.Remove(evidence);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa minh chứng thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/AdminEvidence/ViewImage/5
        public async Task<IActionResult> ViewImage(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evidence = await _context.OfflineAttendanceEvidences.FindAsync(id);
            if (evidence == null || string.IsNullOrEmpty(evidence.FilePath))
            {
                return NotFound();
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var fileExtension = Path.GetExtension(evidence.FileName).ToLowerInvariant();

            if (!imageExtensions.Contains(fileExtension))
            {
                return BadRequest("File này không phải là hình ảnh!");
            }

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, evidence.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = fileExtension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };

            return File(fileBytes, contentType);
        }

        // GET: Admin/AdminEvidence/ExportReport
        public async Task<IActionResult> ExportReport(int? eventId, string? status)
        {
            var query = _context.OfflineAttendanceEvidences
                .Include(e => e.Event)
                .Include(e => e.Attendee)
                .AsQueryable();

            if (eventId.HasValue)
            {
                query = query.Where(e => e.EventId == eventId.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(e => e.Status == status);
            }

            var evidences = await query.OrderByDescending(e => e.UploadedAt).ToListAsync();

            // Tạo CSV report
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("STT,Tên sự kiện,Họ tên,Email,File name,Trạng thái,Lý do từ chối,Ngày upload,Người xử lý,Ngày xử lý");

            int index = 1;
            foreach (var evidence in evidences)
            {
                csv.AppendLine($"{index}," +
                    $"\"{evidence.Event?.Title ?? "N/A"}\"," +
                    $"\"{evidence.Attendee?.FullName ?? "N/A"}\"," +
                    $"{evidence.Attendee?.Email ?? "N/A"}," +
                    $"\"{evidence.FileName}\"," +
                    $"{evidence.Status}," +
                    $"\"{evidence.RejectionReason ?? ""}\"," +
                    $"{evidence.UploadedAt:yyyy-MM-dd HH:mm}," +
                    $"{evidence.ProcessedBy ?? "N/A"}," +
                    $"{(evidence.ProcessedAt.HasValue ? evidence.ProcessedAt.Value.ToString("yyyy-MM-dd HH:mm") : "N/A")}");
                index++;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"BaoCao_MinhChung_{DateTime.Now:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}

