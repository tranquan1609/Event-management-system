using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using DACSWEBSK.Services;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminVideoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly YoutubeDlService _youtubeDlService;
        private readonly HuggingFaceService _huggingFaceService;
        private readonly ILogger<AdminVideoController> _logger;

        public AdminVideoController(
            ApplicationDbContext context,
            YoutubeDlService youtubeDlService,
            HuggingFaceService huggingFaceService,
            ILogger<AdminVideoController> logger)
        {
            _context = context;
            _youtubeDlService = youtubeDlService;
            _huggingFaceService = huggingFaceService;
            _logger = logger;
        }

        // Danh sách video theo sự kiện
        public async Task<IActionResult> Index(int? eventId)
        {
            var events = await _context.Events.OrderByDescending(e => e.StartTime).ToListAsync();
            ViewBag.Events = events;
            ViewBag.CurrentEventId = eventId;

            var videos = _context.Videos.Include(v => v.Event).AsQueryable();
            if (eventId.HasValue)
                videos = videos.Where(v => v.EventId == eventId.Value);

            return View(await videos.ToListAsync());
        }

        // GET: Tạo mới video
        public IActionResult Create(int eventId)
        {
            ViewBag.EventId = eventId;
            return View();
        }

        // POST: Tạo mới video
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Video video)
        {
            if (!ModelState.IsValid)
            {
                foreach (var value in ModelState.Values)
                {
                    foreach (var error in value.Errors)
                    {
                        _logger.LogError("ModelState Error: {ErrorMessage}", error.ErrorMessage);
                    }
                }
                ViewBag.EventId = video.EventId;
                return View(video);
            }

            try
            {
                // Nếu là video YouTube, lấy phụ đề
                if (!string.IsNullOrEmpty(video.Url) && (video.Url.Contains("youtube.com") || video.Url.Contains("youtu.be")))
                {
                    var videoId = _youtubeDlService.ExtractYouTubeVideoId(video.Url);
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        _logger.LogInformation("Processing YouTube video with ID: {VideoId}", videoId);
                        var transcript = await _youtubeDlService.GetTranscriptAsync(videoId);
                        if (!string.IsNullOrEmpty(transcript))
                        {
                            video.Transcript = transcript;
                            _logger.LogInformation("Successfully extracted transcript for video: {VideoId}", videoId);
                        }
                        else
                        {
                            _logger.LogWarning("No transcript available for video: {VideoId}", videoId);
                        }
                    }
                }

                _context.Videos.Add(video);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully created new video with ID: {VideoId}", video.Id);
                return RedirectToAction(nameof(Index), new { eventId = video.EventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving video to database. Transcript length: {Length}", video.Transcript?.Length ?? 0);
                throw;
            }
        }

        // GET: Sửa video
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var video = await _context.Videos.FindAsync(id);
                if (video == null)
                {
                    _logger.LogWarning("Video not found with ID: {VideoId}", id);
                    return NotFound();
                }
                return View(video);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving video for edit with ID: {VideoId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Sửa video
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Video video)
        {
            if (id != video.Id)
            {
                _logger.LogWarning("Video ID mismatch: {Id} != {VideoId}", id, video.Id);
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(video);
            }

            try
            {
                _context.Update(video);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated video with ID: {VideoId}", video.Id);
                return RedirectToAction(nameof(Index), new { eventId = video.EventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating video with ID: {VideoId}", video.Id);
                ModelState.AddModelError("", "An error occurred while updating the video. Please try again.");
                return View(video);
            }
        }

        // Xóa video
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var video = await _context.Videos.FindAsync(id);
                if (video == null)
                {
                    _logger.LogWarning("Video not found with ID: {VideoId}", id);
                    return NotFound();
                }

                var eventId = video.EventId;
                _context.Videos.Remove(video);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully deleted video with ID: {VideoId}", id);
                return RedirectToAction(nameof(Index), new { eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting video with ID: {VideoId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Lấy lại phụ đề cho video
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryTranscript(int id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video == null)
            {
                TempData["Error"] = "Không tìm thấy video.";
                return RedirectToAction(nameof(Index));
            }

            // Chỉ lấy lại phụ đề cho video YouTube
            var videoId = _youtubeDlService.ExtractYouTubeVideoId(video.Url);
            if (string.IsNullOrEmpty(videoId))
            {
                TempData["Error"] = "Không thể xác định video YouTube.";
                return RedirectToAction(nameof(Index), new { eventId = video.EventId });
            }

            var transcript = await _youtubeDlService.GetTranscriptAsync(videoId);
            if (!string.IsNullOrEmpty(transcript))
            {
                video.Transcript = transcript;
                _context.Update(video);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật phụ đề thành công!";
            }
            else
            {
                TempData["Error"] = "Không thể lấy phụ đề. Hãy thử lại sau.";
            }

            return RedirectToAction(nameof(Index), new { eventId = video.EventId });
        }
    }
}
