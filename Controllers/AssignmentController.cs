using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Controllers
{
    public class AssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AssignmentController> _logger;

        public AssignmentController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<AssignmentController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // GET: Assignment/View/5
        public async Task<IActionResult> View(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            // Kiểm tra xem user đã nộp bài chưa
            var userEmail = User.Identity?.Name;
            AssignmentSubmission? existingSubmission = null;

            if (!string.IsNullOrEmpty(userEmail))
            {
                // Tìm attendee theo email và event
                var attendee = await _context.Attendees
                    .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == assignment.EventId && a.TicketStatus == "Confirmed");

                if (attendee != null)
                {
                    existingSubmission = await _context.AssignmentSubmissions
                        .FirstOrDefaultAsync(s => s.AssignmentId == id && s.AttendeeId == attendee.Id);
                }
            }

            ViewBag.ExistingSubmission = existingSubmission;
            ViewBag.UserEmail = userEmail;

            return View(assignment);
        }

        // GET: Assignment/Submit/5
        public async Task<IActionResult> Submit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            // Kiểm tra hạn nộp
            if (assignment.DueDate.HasValue && DateTime.Now > assignment.DueDate.Value)
            {
                TempData["Error"] = "Đã quá hạn nộp bài!";
                return RedirectToAction(nameof(View), new { id });
            }

            // Kiểm tra xem user đã đăng ký và được duyệt chưa
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Vui lòng đăng nhập để nộp bài!";
                return RedirectToAction(nameof(View), new { id });
            }

            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == assignment.EventId && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["Error"] = "Bạn cần đăng ký và được duyệt tham gia sự kiện trước khi nộp bài!";
                return RedirectToAction(nameof(View), new { id });
            }

            // Kiểm tra xem đã nộp bài chưa
            var existingSubmission = await _context.AssignmentSubmissions
                .FirstOrDefaultAsync(s => s.AssignmentId == id && s.AttendeeId == attendee.Id);

            if (existingSubmission != null)
            {
                ViewBag.ExistingSubmission = existingSubmission;
                ViewBag.CanResubmit = true; // Cho phép nộp lại
            }

            ViewBag.Assignment = assignment;
            ViewBag.Attendee = attendee;

            return View();
        }

        // POST: Assignment/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int assignmentId, string? textContent, IFormFile? fileUpload)
        {
            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
            {
                return NotFound();
            }

            // Kiểm tra hạn nộp
            if (assignment.DueDate.HasValue && DateTime.Now > assignment.DueDate.Value)
            {
                TempData["Error"] = "Đã quá hạn nộp bài!";
                return RedirectToAction(nameof(View), new { id = assignmentId });
            }

            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Vui lòng đăng nhập để nộp bài!";
                return RedirectToAction(nameof(View), new { id = assignmentId });
            }

            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == assignment.EventId && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["Error"] = "Bạn cần đăng ký và được duyệt tham gia sự kiện trước khi nộp bài!";
                return RedirectToAction(nameof(View), new { id = assignmentId });
            }

            // Kiểm tra xem có nội dung nào được gửi không
            if (string.IsNullOrWhiteSpace(textContent) && (fileUpload == null || fileUpload.Length == 0))
            {
                TempData["Error"] = "Vui lòng nhập văn bản hoặc upload file!";
                return RedirectToAction(nameof(Submit), new { id = assignmentId });
            }

            // Kiểm tra quyền nộp
            if (!string.IsNullOrWhiteSpace(textContent) && !assignment.AllowTextSubmission)
            {
                TempData["Error"] = "Bài thu hoạch này không cho phép nộp văn bản!";
                return RedirectToAction(nameof(Submit), new { id = assignmentId });
            }

            if (fileUpload != null && fileUpload.Length > 0 && !assignment.AllowFileUpload)
            {
                TempData["Error"] = "Bài thu hoạch này không cho phép upload file!";
                return RedirectToAction(nameof(Submit), new { id = assignmentId });
            }

            // Kiểm tra xem đã nộp bài chưa
            var existingSubmission = await _context.AssignmentSubmissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.AttendeeId == attendee.Id);

            string? filePath = null;
            string? fileName = null;

            // Xử lý file upload
            if (fileUpload != null && fileUpload.Length > 0)
            {
                // Kiểm tra kích thước file (max 10MB)
                if (fileUpload.Length > 10 * 1024 * 1024)
                {
                    TempData["Error"] = "File không được vượt quá 10MB!";
                    return RedirectToAction(nameof(Submit), new { id = assignmentId });
                }

                // Kiểm tra extension
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(fileUpload.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["Error"] = "Chỉ chấp nhận file: PDF, DOC, DOCX, TXT, JPG, JPEG, PNG!";
                    return RedirectToAction(nameof(Submit), new { id = assignmentId });
                }

                // Tạo thư mục lưu file
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "assignments");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Tạo tên file duy nhất
                fileName = $"{assignmentId}_{attendee.Id}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                filePath = Path.Combine(uploadsFolder, fileName);

                // Xóa file cũ nếu có
                if (existingSubmission != null && !string.IsNullOrEmpty(existingSubmission.FilePath))
                {
                    var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, existingSubmission.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not delete old file: {ex.Message}");
                        }
                    }
                }

                // Lưu file mới
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await fileUpload.CopyToAsync(fileStream);
                }

                filePath = $"/uploads/assignments/{fileName}";
            }

            // Tạo hoặc cập nhật submission
            if (existingSubmission != null)
            {
                // Cập nhật bài nộp cũ
                existingSubmission.TextContent = textContent;
                if (!string.IsNullOrEmpty(filePath))
                {
                    existingSubmission.FilePath = filePath;
                    existingSubmission.FileName = fileUpload?.FileName;
                }
                existingSubmission.SubmittedAt = DateTime.Now;
                existingSubmission.Status = "Submitted";
                existingSubmission.Score = null; // Reset điểm khi nộp lại
                existingSubmission.Feedback = null;
                existingSubmission.GradedAt = null;
                existingSubmission.GradedBy = null;

                _context.Update(existingSubmission);
            }
            else
            {
                // Tạo bài nộp mới
                var submission = new AssignmentSubmission
                {
                    AssignmentId = assignmentId,
                    AttendeeId = attendee.Id,
                    TextContent = textContent,
                    FilePath = filePath,
                    FileName = fileUpload?.FileName,
                    Status = "Submitted",
                    SubmittedAt = DateTime.Now
                };

                _context.Add(submission);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Nộp bài thành công!";
            return RedirectToAction(nameof(View), new { id = assignmentId });
        }

        // GET: Assignment/MySubmission/5
        public async Task<IActionResult> MySubmission(int? id)
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

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.Email == userEmail && a.EventId == assignment.EventId && a.TicketStatus == "Confirmed");

            if (attendee == null)
            {
                TempData["Error"] = "Bạn chưa đăng ký tham gia sự kiện này!";
                return RedirectToAction("Details", "Event", new { id = assignment.EventId });
            }

            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.AssignmentId == id && s.AttendeeId == attendee.Id);

            if (submission == null)
            {
                TempData["Error"] = "Bạn chưa nộp bài!";
                return RedirectToAction(nameof(View), new { id });
            }

            return View(submission);
        }

        // GET: Assignment/DownloadFile/5
        public async Task<IActionResult> DownloadFile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Attendee)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null || string.IsNullOrEmpty(submission.FilePath))
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
            if (submission.Attendee?.Email != userEmail && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, submission.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = submission.FileName ?? "submission_file";
            return File(fileBytes, "application/octet-stream", fileName);
        }
    }
}

