using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.AspNetCore.Hosting;
using DACSWEBSK.Services;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IO;
using System.Linq;

namespace DACSWEBSK.Controllers
{
    [Authorize]
    public class VideoController : Controller
    {
        private readonly IVideoRepository _videoRepository;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly HuggingFaceService _huggingFaceService;
        private readonly ILogger<VideoController> _logger;
        private readonly YoutubeDlService _transcriptService;
        private readonly IVideoProgressService _videoProgressService;
        private readonly ApplicationDbContext _context;
        
        public VideoController(
            IVideoRepository videoRepository,
            IWebHostEnvironment webHostEnvironment,
            HuggingFaceService huggingFaceService,
            ILogger<VideoController> logger,
            YoutubeDlService transcriptService,
            IVideoProgressService videoProgressService,
            ApplicationDbContext context)
        {
            _videoRepository = videoRepository;
            _webHostEnvironment = webHostEnvironment;
            _huggingFaceService = huggingFaceService;
            _logger = logger;
            _transcriptService = transcriptService;
            _videoProgressService = videoProgressService;
            _context = context;
        }

        public IActionResult Index()
        {
            var videos = _videoRepository.GetAllVideos();
            return View(videos);
        }

        public async Task<IActionResult> Details(int id)
        {
            var video = _videoRepository.GetVideoById(id);
            if (video == null)
            {
                return NotFound();
            }

            // Debug: Log video URL để kiểm tra
            _logger.LogInformation("Video Details - ID: {VideoId}, Title: {Title}, URL: {Url}, HasUrl: {HasUrl}", 
                video.Id, video.Title, video.Url ?? "NULL", !string.IsNullOrEmpty(video.Url));

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
            {
                // User is not logged in, or email is not available. Video vẫn hiển thị nhưng không tracking
                ViewBag.InitialViewDuration = 0;
                ViewBag.InitialViewPercentage = 0;
                ViewBag.AttendeeId = null;
                ViewBag.IsApproved = false; // Chỉ dùng để biết có tracking hay không
                ViewBag.CanTrackProgress = false;
                return View(video);
            }

            // Lấy attendee ID từ user hiện tại bằng email
            var attendee = await _context.Attendees
                .FirstOrDefaultAsync(a => a.EventId == video.EventId &&
                                        a.Email == userEmail);

            if (attendee == null)
            {
                // User chưa đăng ký tham gia sự kiện này. Video vẫn hiển thị nhưng không tracking
                ViewBag.InitialViewDuration = 0;
                ViewBag.InitialViewPercentage = 0;
                ViewBag.AttendeeId = null;
                ViewBag.IsApproved = false;
                ViewBag.CanTrackProgress = false;
                return View(video);
            }

            // Kiểm tra xem attendee đã được duyệt chưa (chỉ ảnh hưởng đến tracking, không ảnh hưởng đến việc xem video)
            bool isApproved = attendee.TicketStatus == "Confirmed";
            
            if (isApproved)
            {
                // Attendee đã được duyệt, cho phép tracking progress
                ViewBag.AttendeeId = attendee.Id;
                ViewBag.IsApproved = true;
                ViewBag.CanTrackProgress = true;
                
                // Get initial video progress for the current user
                var videoProgress = await _videoProgressService.GetProgressAsync(video.Id, attendee.Id);
                ViewBag.InitialViewDuration = videoProgress?.ViewDuration ?? 0;
                ViewBag.InitialViewPercentage = videoProgress?.ViewPercentage ?? 0;
            }
            else
            {
                // User đã đăng ký nhưng chưa được duyệt. Video vẫn hiển thị nhưng không tracking
                ViewBag.InitialViewDuration = 0;
                ViewBag.InitialViewPercentage = 0;
                ViewBag.AttendeeId = null;
                ViewBag.IsApproved = false;
                ViewBag.CanTrackProgress = false;
                ViewBag.TicketStatus = attendee.TicketStatus;
            }

            return View(video);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Video/GenerateSummary/{id}")]
        public async Task<IActionResult> GenerateSummary(int id)
        {
            var video = _videoRepository.GetVideoById(id);
            if (video == null)
            {
                _logger.LogWarning("Video not found for summary generation: {VideoId}", id);
                return Json(new { success = false, message = "Không tìm thấy video" });
            }

            try
            {
                // Lấy transcript từ database
                if (string.IsNullOrEmpty(video.Transcript))
                {
                    _logger.LogWarning("No transcript found for video: {VideoId}", id);
                    return Json(new { success = false, message = "Không tìm thấy phụ đề trong database" });
                }

                _logger.LogInformation("Generating summary for video: {VideoId}, transcript length: {Length}", id, video.Transcript.Length);

                // Gửi transcript cho HuggingFace để tóm tắt (dùng hàm chia nhỏ)
                var summary = await _huggingFaceService.SummarizeLongTextAsync(video.Transcript);

                if (string.IsNullOrWhiteSpace(summary))
                {
                    _logger.LogWarning("Empty summary generated for video: {VideoId}", id);
                    return Json(new { success = false, message = "Không thể tạo tóm tắt. Vui lòng thử lại sau." });
                }

                // Lưu lại summary vào database
                video.Summary = summary;
                _videoRepository.UpdateVideo(video);

                _logger.LogInformation("Successfully generated and saved summary for video: {VideoId}", id);
                return Json(new { success = true, summary = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary for video: {VideoId}", id);
                return Json(new { success = false, message = "Không thể tạo tóm tắt: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile videoFile, string title, string description, string summary, int eventId)
        {
            if (videoFile != null && videoFile.Length > 0)
            {
                // Tạo tên file duy nhất
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + videoFile.FileName;
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "videos");
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Lưu file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(fileStream);
                }

                // Tạo video mới
                var video = new Video
                {
                    Title = title,
                    Description = description,
                    Summary = summary,
                    LocalFilePath = "/videos/" + uniqueFileName,
                    EventId = eventId,
                    UploadDate = DateTime.Now
                };

                if (!string.IsNullOrEmpty(video.Url) && (video.Url.Contains("youtube.com") || video.Url.Contains("youtu.be")))
                {
                    var videoId = _transcriptService.ExtractYouTubeVideoId(video.Url);
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        _logger.LogInformation("Processing YouTube video with ID: {VideoId}", videoId);
                        var transcript = await _transcriptService.GetTranscriptAsync(videoId);

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

                _videoRepository.AddVideo(video);

                // Tự động tạo tóm tắt nếu chưa có
                if (string.IsNullOrEmpty(summary))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(video.Transcript))
                            video.Summary = await _huggingFaceService.SummarizeLongTextAsync(video.Transcript);
                        _videoRepository.UpdateVideo(video);
                    }
                    catch
                    {
                        // Bỏ qua lỗi khi tạo tóm tắt tự động
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return BadRequest("No file uploaded");
        }

        [HttpPost]
        public async Task<IActionResult> Create(Video video)
        {
            try
            {
                _logger.LogInformation("Starting video creation process for URL: {Url}", video.Url);

                // Lấy transcript từ YouTube
                _logger.LogInformation("Attempting to get transcript from YouTube...");
                var videoId = _transcriptService.ExtractYouTubeVideoId(video.Url);
                var transcript = await _transcriptService.GetTranscriptAsync(videoId);
                _logger.LogInformation("Transcript retrieved successfully. Length: {Length}", transcript?.Length ?? 0);

                if (string.IsNullOrEmpty(transcript))
                {
                    _logger.LogWarning("No transcript was retrieved from YouTube");
                    ModelState.AddModelError("", "Could not retrieve transcript from YouTube video");
                    return View(video);
                }

                video.Transcript = transcript;

                // Tạo tóm tắt từ transcript
                _logger.LogInformation("Generating summary from transcript...");
                var summary = await _huggingFaceService.SummarizeAsync(transcript);
                _logger.LogInformation("Summary generated successfully. Length: {Length}", summary?.Length ?? 0);

                video.Summary = summary;

                // Lưu video vào database
                _logger.LogInformation("Saving video to database...");
                _videoRepository.AddVideo(video);
                _logger.LogInformation("Video saved successfully with ID: {Id}", video.Id);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing video: {Message}", ex.Message);
                ModelState.AddModelError("", "Error processing video: " + ex.Message);
                return View(video);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Video/TranslateSubtitle/{id}")]
        public async Task<IActionResult> TranslateSubtitle(int id, [FromBody] TranslateSubtitleRequest request)
        {
            try
            {
                var video = _videoRepository.GetVideoById(id);
                if (video == null)
                {
                    _logger.LogWarning("Video not found for translation: {VideoId}", id);
                    return Json(new { success = false, message = "Không tìm thấy video" });
                }

                if (string.IsNullOrEmpty(video.Transcript))
                {
                    _logger.LogWarning("No transcript found for video: {VideoId}", id);
                    return Json(new { success = false, message = "Không tìm thấy phụ đề trong database" });
                }

                if (string.IsNullOrEmpty(request?.TargetLanguage))
                {
                    return Json(new { success = false, message = "Vui lòng chọn ngôn ngữ đích" });
                }

                _logger.LogInformation("Translating subtitle for video: {VideoId}, target language: {TargetLang}", 
                    id, request.TargetLanguage);

                // Kiểm tra xem đã có bản dịch chưa
                var existingTranslation = await _context.SubtitleTranslations
                    .FirstOrDefaultAsync(st => st.VideoId == id && st.TargetLanguage == request.TargetLanguage);

                if (existingTranslation != null)
                {
                    _logger.LogInformation("Translation already exists for video: {VideoId}, language: {TargetLang}", 
                        id, request.TargetLanguage);
                    return Json(new { 
                        success = true, 
                        translatedText = existingTranslation.TranslatedText,
                        language = existingTranslation.TargetLanguage,
                        cached = true
                    });
                }

                // Dịch phụ đề
                var translatedText = await _huggingFaceService.TranslateAsync(
                    video.Transcript, 
                    request.TargetLanguage);

                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    _logger.LogWarning("Empty translation result for video: {VideoId}", id);
                    return Json(new { success = false, message = "Không thể dịch phụ đề. Vui lòng thử lại sau." });
                }

                // Lưu bản dịch vào database
                var translation = new SubtitleTranslation
                {
                    VideoId = id,
                    TargetLanguage = request.TargetLanguage,
                    TranslatedText = translatedText,
                    CreatedAt = DateTime.Now
                };

                _context.SubtitleTranslations.Add(translation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully translated and saved subtitle for video: {VideoId}, language: {TargetLang}", 
                    id, request.TargetLanguage);

                return Json(new { 
                    success = true, 
                    translatedText = translatedText,
                    language = request.TargetLanguage,
                    cached = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating subtitle for video: {VideoId}", id);
                return Json(new { success = false, message = "Đã xảy ra lỗi khi dịch phụ đề: " + ex.Message });
            }
        }

        [HttpGet]
        [Route("Video/GetTranslations/{id}")]
        public async Task<IActionResult> GetTranslations(int id)
        {
            try
            {
                var translations = await _context.SubtitleTranslations
                    .Where(st => st.VideoId == id)
                    .Select(st => new { 
                        language = st.TargetLanguage, 
                        translatedText = st.TranslatedText,
                        createdAt = st.CreatedAt
                    })
                    .ToListAsync();

                return Json(new { success = true, translations = translations });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting translations for video: {VideoId}", id);
                return Json(new { success = false, message = "Đã xảy ra lỗi khi lấy danh sách bản dịch" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress([FromBody] VideoProgressUpdateModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state in UpdateProgress: {Errors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var video = await _context.Videos
                    .FirstOrDefaultAsync(v => v.Id == model.VideoId);

                if (video == null)
                {
                    _logger.LogWarning("Video not found: {VideoId}", model.VideoId);
                    return Json(new { success = false, message = "Video không tồn tại" });
                }

                // Lấy attendee ID từ user hiện tại
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning("User email not found in claims");
                    return Json(new { success = false, message = "Vui lòng đăng nhập để cập nhật tiến độ" });
                }

                var attendee = await _context.Attendees
                    .FirstOrDefaultAsync(a => a.EventId == video.EventId &&
                                            a.Email == userEmail);

                if (attendee == null)
                {
                    _logger.LogWarning("Attendee not found for email: {Email}, eventId: {EventId}", userEmail, video.EventId);
                    return Json(new { success = false, message = "Bạn chưa đăng ký tham gia sự kiện này" });
                }

                // Kiểm tra xem attendee đã được duyệt chưa
                if (attendee.TicketStatus != "Confirmed")
                {
                    _logger.LogWarning("Attendee not approved for email: {Email}, eventId: {EventId}, status: {Status}", 
                        userEmail, video.EventId, attendee.TicketStatus);
                    return Json(new { success = false, message = "Bạn cần được duyệt tham gia sự kiện trước khi xem video" });
                }

                // Cập nhật tiến độ xem video
                await _videoProgressService.UpdateVideoProgressAsync(
                    model.VideoId,
                    attendee.Id,
                    model.CurrentTime);

                // Kiểm tra điều kiện cấp chứng nhận
                var isEligible = await _videoProgressService.CheckCertificateEligibilityAsync(
                    attendee.Id,
                    video.EventId);

                _logger.LogInformation("Progress updated successfully for video: {VideoId}, attendee: {AttendeeId}, time: {CurrentTime}", 
                    model.VideoId, attendee.Id, model.CurrentTime);

                return Json(new { success = true, isEligibleForCertificate = isEligible });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating video progress for video: {VideoId}", model?.VideoId);
                return Json(new { success = false, message = "Đã xảy ra lỗi khi cập nhật tiến độ: " + ex.Message });
            }
        }
    }

    public class VideoProgressUpdateModel
    {
        public int VideoId { get; set; }
        public int CurrentTime { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class TranslateSubtitleRequest
    {
        public string TargetLanguage { get; set; } = string.Empty;
    }
}
