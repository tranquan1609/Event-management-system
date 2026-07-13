using DACSWEBSK.Models;
using DACSWEBSK.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Controllers
{
    [Authorize]
    public class EvidenceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<EvidenceController> _logger;

        public EvidenceController(
            ApplicationDbContext context,
            IFileStorageService fileStorage,
            ILogger<EvidenceController> logger)
        {
            _context = context;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        // GET: Evidence/Upload/5
        public async Task<IActionResult> Upload(int? id)
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

            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Vui lòng đăng nhập để upload minh chứng!";
                return RedirectToAction("Details", "Event", new { id });
            }

            // Kiểm tra xem user đã đăng ký và được duyệt chưa
            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == id && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["Error"] = "Bạn cần đăng ký và được duyệt tham gia sự kiện trước khi upload minh chứng!";
                return RedirectToAction("Details", "Event", new { id });
            }

            // Kiểm tra xem đã upload minh chứng chưa
            var existingEvidences = await _context.OfflineAttendanceEvidences
                .Where(e => e.EventId == id && e.AttendeeId == attendee.Id)
                .OrderByDescending(e => e.UploadedAt)
                .ToListAsync();

            ViewBag.Event = evt;
            ViewBag.Attendee = attendee;
            ViewBag.ExistingEvidences = existingEvidences;

            return View();
        }

        // POST: Evidence/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int eventId, List<IFormFile> files)
        {
            var evt = await _context.Events.FindAsync(eventId);
            if (evt == null)
            {
                return NotFound();
            }

            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Vui lòng đăng nhập để upload minh chứng!";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == eventId && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["Error"] = "Bạn cần đăng ký và được duyệt tham gia sự kiện trước khi upload minh chứng!";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            if (files == null || files.Count == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một file để upload!";
                return RedirectToAction(nameof(Upload), new { id = eventId });
            }

            // Kiểm tra số lượng file (tối đa 10 file)
            if (files.Count > 10)
            {
                TempData["Error"] = "Chỉ được upload tối đa 10 file!";
                return RedirectToAction(nameof(Upload), new { id = eventId });
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            const long maxFileSize = 10 * 1024 * 1024; // 10MB

            var uploadedFiles = new List<OfflineAttendanceEvidence>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                    continue;

                // Kiểm tra kích thước file
                if (file.Length > maxFileSize)
                {
                    errors.Add($"File {file.FileName} vượt quá 10MB!");
                    continue;
                }

                // Kiểm tra extension
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    errors.Add($"File {file.FileName} không đúng định dạng! Chỉ chấp nhận: PDF, DOC, DOCX, JPG, JPEG, PNG, GIF, BMP");
                    continue;
                }

                try
                {
                    var storedPath = await _fileStorage.UploadAsync(file, StorageCategory.Evidence);

                    var evidence = new OfflineAttendanceEvidence
                    {
                        EventId = eventId,
                        AttendeeId = attendee.Id,
                        FilePath = storedPath,
                        FileName = file.FileName,
                        FileType = fileExtension,
                        FileSize = file.Length,
                        Status = "Pending",
                        UploadedAt = DateTime.Now
                    };

                    uploadedFiles.Add(evidence);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error uploading file {file.FileName}");
                    errors.Add($"Lỗi khi upload file {file.FileName}: {ex.Message}");
                }
            }

            if (uploadedFiles.Count > 0)
            {
                _context.OfflineAttendanceEvidences.AddRange(uploadedFiles);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã upload thành công {uploadedFiles.Count} file minh chứng!";
            }

            if (errors.Count > 0)
            {
                TempData["Error"] = string.Join("<br/>", errors);
            }

            return RedirectToAction(nameof(Upload), new { id = eventId });
        }

        // GET: Evidence/MyEvidence/5
        public async Task<IActionResult> MyEvidence(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Vui lòng đăng nhập!";
                return RedirectToAction("Index", "Event");
            }

            var evt = await _context.Events.FindAsync(id);
            if (evt == null)
            {
                return NotFound();
            }

            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == id && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["Error"] = "Bạn chưa đăng ký tham gia sự kiện này!";
                return RedirectToAction("Details", "Event", new { id });
            }

            var evidences = await _context.OfflineAttendanceEvidences
                .Include(e => e.Event)
                .Where(e => e.EventId == id && e.AttendeeId == attendee.Id)
                .OrderByDescending(e => e.UploadedAt)
                .ToListAsync();

            ViewBag.Event = evt;
            ViewBag.Attendee = attendee;

            return View(evidences);
        }

        // GET: Evidence/DownloadFile/5
        public async Task<IActionResult> DownloadFile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evidence = await _context.OfflineAttendanceEvidences
                .Include(e => e.Attendee)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evidence == null || string.IsNullOrEmpty(evidence.FilePath))
            {
                return NotFound();
            }

            // Kiểm tra quyền truy cập
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized();
            }

            // Chỉ cho phép download file của chính mình hoặc admin
            if (evidence.Attendee?.Email != userEmail && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var fileResult = await FileStorageResults.TryFileResultAsync(
                _fileStorage,
                evidence.FilePath,
                evidence.FileName);
            if (fileResult == null)
            {
                return NotFound();
            }

            return fileResult;
        }

        // POST: Evidence/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var evidence = await _context.OfflineAttendanceEvidences
                .Include(e => e.Attendee)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evidence == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail) || evidence.Attendee?.Email != userEmail)
            {
                return Forbid();
            }

            // Chỉ cho phép xóa nếu chưa được duyệt
            if (evidence.Status != "Pending")
            {
                TempData["Error"] = "Không thể xóa minh chứng đã được xử lý!";
                return RedirectToAction(nameof(MyEvidence), new { id = evidence.EventId });
            }

            // Xóa file vật lý
            if (!string.IsNullOrEmpty(evidence.FilePath))
            {
                try
                {
                    await _fileStorage.DeleteAsync(evidence.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not delete file {evidence.FilePath}: {ex.Message}");
                }
            }

            _context.OfflineAttendanceEvidences.Remove(evidence);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa minh chứng thành công!";
            return RedirectToAction(nameof(MyEvidence), new { id = evidence.EventId });
        }
    }
}

