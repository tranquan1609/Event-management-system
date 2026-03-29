using DACSWEBSK.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class AdminAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AdminAssignmentController> _logger;

        public AdminAssignmentController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<AdminAssignmentController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // GET: Admin/AdminAssignment
        public async Task<IActionResult> Index(int? eventId)
        {
            var query = _context.EventAssignments
                .Include(a => a.Event)
                .Include(a => a.Submissions)
                .AsQueryable();

            if (eventId.HasValue)
            {
                query = query.Where(a => a.EventId == eventId.Value);
            }

            ViewBag.Events = await _context.Events.OrderBy(e => e.StartTime).ToListAsync();
            ViewBag.CurrentEventId = eventId;

            var assignments = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            return View(assignments);
        }

        // GET: Admin/AdminAssignment/Create
        public async Task<IActionResult> Create(int? eventId)
        {
            ViewBag.Events = new SelectList(
                await _context.Events.OrderBy(e => e.StartTime).ToListAsync(),
                "Id",
                "Title",
                eventId);

            if (eventId.HasValue)
            {
                ViewBag.SelectedEventId = eventId.Value;
            }

            return View();
        }

        // POST: Admin/AdminAssignment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EventId,Title,Description,Instructions,DueDate,MaxScore,AllowFileUpload,AllowTextSubmission")] EventAssignment assignment)
        {
            if (ModelState.IsValid)
            {
                assignment.CreatedAt = DateTime.Now;
                _context.Add(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Tạo bài thu hoạch thành công!";
                return RedirectToAction(nameof(Index), new { eventId = assignment.EventId });
            }

            ViewBag.Events = new SelectList(
                await _context.Events.OrderBy(e => e.StartTime).ToListAsync(),
                "Id",
                "Title",
                assignment.EventId);

            return View(assignment);
        }

        // GET: Admin/AdminAssignment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments.FindAsync(id);
            if (assignment == null)
            {
                return NotFound();
            }

            ViewBag.Events = new SelectList(
                await _context.Events.OrderBy(e => e.StartTime).ToListAsync(),
                "Id",
                "Title",
                assignment.EventId);

            return View(assignment);
        }

        // POST: Admin/AdminAssignment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EventId,Title,Description,Instructions,DueDate,MaxScore,AllowFileUpload,AllowTextSubmission,CreatedAt")] EventAssignment assignment)
        {
            if (id != assignment.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    assignment.UpdatedAt = DateTime.Now;
                    _context.Update(assignment);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật bài thu hoạch thành công!";
                    return RedirectToAction(nameof(Index), new { eventId = assignment.EventId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventAssignmentExists(assignment.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Events = new SelectList(
                await _context.Events.OrderBy(e => e.StartTime).ToListAsync(),
                "Id",
                "Title",
                assignment.EventId);

            return View(assignment);
        }

        // GET: Admin/AdminAssignment/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Attendee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        // GET: Admin/AdminAssignment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        // POST: Admin/AdminAssignment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.EventAssignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment != null)
            {
                // Xóa các file đã upload
                foreach (var submission in assignment.Submissions)
                {
                    if (!string.IsNullOrEmpty(submission.FilePath))
                    {
                        var filePath = Path.Combine(_webHostEnvironment.WebRootPath, submission.FilePath.TrimStart('/'));
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
                }

                // Xóa các submissions
                _context.AssignmentSubmissions.RemoveRange(assignment.Submissions);
                
                // Xóa assignment
                _context.EventAssignments.Remove(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa bài thu hoạch thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/AdminAssignment/ManageSubmissions/5
        public async Task<IActionResult> ManageSubmissions(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Attendee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        // GET: Admin/AdminAssignment/DownloadFile/5
        public async Task<IActionResult> DownloadFile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var submission = await _context.AssignmentSubmissions.FindAsync(id);
            if (submission == null || string.IsNullOrEmpty(submission.FilePath))
            {
                return NotFound();
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

        // POST: Admin/AdminAssignment/GradeSubmission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GradeSubmission(int submissionId, decimal? score, string? feedback)
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .Include(s => s.Attendee)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
            {
                return NotFound();
            }

            // Kiểm tra xem đã chấm điểm chưa để tránh cộng điểm nhiều lần
            bool isFirstTimeGrading = !submission.Score.HasValue || submission.Status != "Graded";
            decimal? previousScore = submission.Score;

            if (score.HasValue)
            {
                submission.Score = score.Value;
            }

            if (!string.IsNullOrEmpty(feedback))
            {
                submission.Feedback = feedback;
            }

            submission.Status = "Graded";
            submission.GradedAt = DateTime.Now;
            submission.GradedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";

            // Cộng điểm vào Points của user nếu có điểm số
            if (score.HasValue && submission.Attendee != null && !string.IsNullOrEmpty(submission.Attendee.Email))
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == submission.Attendee.Email);

                if (user != null)
                {
                    // Nếu là lần đầu chấm điểm, cộng điểm mới
                    // Nếu đã chấm rồi, chỉ cộng phần chênh lệch (điểm mới - điểm cũ)
                    if (isFirstTimeGrading)
                    {
                        // Lần đầu chấm: cộng toàn bộ điểm
                        user.Points += (int)Math.Round(score.Value);
                        _logger.LogInformation($"Cộng {score.Value} điểm cho user {user.Email} (lần đầu chấm điểm)");
                    }
                    else if (previousScore.HasValue)
                    {
                        // Đã chấm rồi: chỉ cộng phần chênh lệch
                        decimal scoreDifference = score.Value - previousScore.Value;
                        if (scoreDifference > 0)
                        {
                            user.Points += (int)Math.Round(scoreDifference);
                            _logger.LogInformation($"Cộng thêm {scoreDifference} điểm cho user {user.Email} (điều chỉnh điểm từ {previousScore.Value} lên {score.Value})");
                        }
                        else if (scoreDifference < 0)
                        {
                            // Nếu điểm giảm, trừ điểm (nhưng không để Points âm)
                            int pointsToDeduct = (int)Math.Round(Math.Abs(scoreDifference));
                            user.Points = Math.Max(0, user.Points - pointsToDeduct);
                            _logger.LogInformation($"Trừ {pointsToDeduct} điểm của user {user.Email} (điều chỉnh điểm từ {previousScore.Value} xuống {score.Value})");
                        }
                    }

                    _context.Users.Update(user);
                }
            }

            _context.Update(submission);
            await _context.SaveChangesAsync();

            if (score.HasValue && isFirstTimeGrading)
            {
                TempData["Success"] = $"Chấm điểm thành công! Đã cộng {score.Value} điểm vào tài khoản của người nộp bài.";
            }
            else if (score.HasValue && previousScore.HasValue && score.Value != previousScore.Value)
            {
                decimal difference = score.Value - previousScore.Value;
                if (difference > 0)
                {
                    TempData["Success"] = $"Cập nhật điểm thành công! Đã cộng thêm {difference} điểm vào tài khoản của người nộp bài.";
                }
                else
                {
                    TempData["Success"] = $"Cập nhật điểm thành công! Đã trừ {Math.Abs(difference)} điểm từ tài khoản của người nộp bài.";
                }
            }
            else
            {
                TempData["Success"] = "Chấm điểm thành công!";
            }

            return RedirectToAction(nameof(ManageSubmissions), new { id = submission.AssignmentId });
        }

        // GET: Admin/AdminAssignment/ExportReport/5
        public async Task<IActionResult> ExportReport(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.EventAssignments
                .Include(a => a.Event)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Attendee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            // Tạo CSV report
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("STT,Họ tên,Email,Điểm số,Nhận xét,Trạng thái,Ngày nộp");
            
            int index = 1;
            foreach (var submission in assignment.Submissions.OrderBy(s => s.SubmittedAt))
            {
                csv.AppendLine($"{index},{submission.Attendee?.FullName ?? "N/A"},{submission.Attendee?.Email ?? "N/A"},{submission.Score?.ToString() ?? "Chưa chấm"},\"{submission.Feedback ?? ""}\",{submission.Status},{submission.SubmittedAt:yyyy-MM-dd HH:mm}");
                index++;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"BaoCao_BaiThuHoach_{assignment.Event?.Title?.Replace(" ", "_") ?? "Event"}_{DateTime.Now:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }

        private bool EventAssignmentExists(int id)
        {
            return _context.EventAssignments.Any(e => e.Id == id);
        }
    }
}

