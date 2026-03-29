using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Services
{
    public class VideoProgressService : IVideoProgressService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VideoProgressService> _logger;
        private const double COMPLETION_THRESHOLD = 70.0; // 70% threshold

        public VideoProgressService(
            ApplicationDbContext context,
            ILogger<VideoProgressService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<VideoProgress> GetProgressAsync(int videoId, int attendeeId)
        {
            var progress = await _context.VideoProgresses
                .FirstOrDefaultAsync(vp => vp.VideoId == videoId && vp.AttendeeId == attendeeId);

            if (progress == null)
            {
                progress = new VideoProgress
                {
                    VideoId = videoId,
                    AttendeeId = attendeeId,
                    ViewDuration = 0,
                    ViewPercentage = 0
                };
                _context.VideoProgresses.Add(progress);
                await _context.SaveChangesAsync();
            }

            return progress;
        }

        public async Task UpdateVideoProgressAsync(int videoId, int attendeeId, int currentTime)
        {
            var video = await _context.Videos.FindAsync(videoId);
            if (video == null) 
            {
                _logger.LogWarning($"Video {videoId} không tồn tại");
                throw new ArgumentException("Video không tồn tại");
            }

            if (video.Duration <= 0)
            {
                _logger.LogWarning($"Video {videoId} chưa có thời lượng hợp lệ");
                throw new ArgumentException("Video chưa có thời lượng hợp lệ");
            }

            var progress = await GetProgressAsync(videoId, attendeeId);
            
            // Cập nhật thời lượng xem và phần trăm
            progress.ViewDuration = Math.Max(progress.ViewDuration, currentTime);
            progress.ViewPercentage = (progress.ViewDuration * 100.0) / video.Duration;
            progress.LastViewedAt = DateTime.Now;

            // Kiểm tra hoàn thành
            if (progress.ViewPercentage >= COMPLETION_THRESHOLD && !progress.IsCompleted)
            {
                progress.IsCompleted = true;
                await CheckAndUpdateCertificateEligibility(progress.AttendeeId, video.EventId);
                _logger.LogInformation($"Attendee {attendeeId} đã hoàn thành video {videoId}");
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateLivestreamProgressAsync(int videoId, int attendeeId, DateTime? joinTime, DateTime? leaveTime)
        {
            var video = await _context.Videos.FindAsync(videoId);
            if (video == null || video.Type != VideoType.Livestream)
                throw new ArgumentException("Video không tồn tại hoặc không phải là livestream");

            var progress = await GetProgressAsync(videoId, attendeeId);

            if (joinTime.HasValue)
            {
                progress.JoinTime = joinTime;
            }

            if (leaveTime.HasValue)
            {
                progress.LeaveTime = leaveTime;
                
                // Tính toán thời gian tham gia
                if (progress.JoinTime.HasValue)
                {
                    var eventStart = video.StartTime ?? video.Event.StartTime;
                    var eventEnd = video.EndTime ?? video.Event.EndTime;
                    
                    var effectiveJoinTime = progress.JoinTime.Value > eventStart ? progress.JoinTime.Value : eventStart;
                    var effectiveLeaveTime = leaveTime.Value < eventEnd ? leaveTime.Value : eventEnd;
                    
                    progress.TotalAttendanceTime = (int)effectiveLeaveTime.Subtract(effectiveJoinTime).TotalSeconds;
                    progress.AttendancePercentage = (progress.TotalAttendanceTime.Value * 100.0) / 
                        (eventEnd - eventStart).TotalSeconds;

                    // Kiểm tra hoàn thành
                    if (progress.AttendancePercentage >= COMPLETION_THRESHOLD && !progress.IsCompleted)
                    {
                        progress.IsCompleted = true;
                        await CheckAndUpdateCertificateEligibility(progress.AttendeeId, video.EventId);
                        _logger.LogInformation($"Attendee {attendeeId} đã hoàn thành livestream {videoId}");
                    }
                }
            }

            progress.LastViewedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CheckCertificateEligibilityAsync(int attendeeId, int eventId)
        {
            // Lấy thông tin người tham gia
            var attendee = await _context.Attendees.FindAsync(attendeeId);
            if (attendee == null)
            {
                _logger.LogWarning($"Không tìm thấy attendee {attendeeId}");
                return false;
            }

            // Lấy danh sách video bắt buộc
            var requiredVideos = await _context.Videos
                .Where(v => v.EventId == eventId && v.IsRequired)
                .ToListAsync();

            // Nếu không có video bắt buộc, người tham gia đủ điều kiện nhận chứng nhận
            if (requiredVideos.Count == 0)
            {
                _logger.LogInformation($"Event {eventId} không có video bắt buộc, attendee {attendeeId} đủ điều kiện nhận chứng nhận");
                
                if (!attendee.IsEligibleForCertificate)
                {
                    attendee.IsEligibleForCertificate = true;
                    await _context.SaveChangesAsync();
                }
                
                return true;
            }

            // Kiểm tra tiến độ xem video
            var progresses = await _context.VideoProgresses
                .Where(vp => vp.AttendeeId == attendeeId && 
                           requiredVideos.Select(v => v.Id).Contains(vp.VideoId))
                .ToListAsync();

            var isEligible = progresses.Count == requiredVideos.Count &&
                           progresses.All(p => p.IsCompleted);

            // Cập nhật trạng thái đủ điều kiện
            if (attendee.IsEligibleForCertificate != isEligible)
            {
                attendee.IsEligibleForCertificate = isEligible;
                await _context.SaveChangesAsync();
                
                if (isEligible)
                {
                    _logger.LogInformation($"Attendee {attendeeId} đã đủ điều kiện nhận chứng nhận cho event {eventId}");
                }
            }

            return isEligible;
        }

        public async Task<IEnumerable<VideoProgress>> GetAttendeeProgressesAsync(int attendeeId, int eventId)
        {
            return await _context.VideoProgresses
                .Include(vp => vp.Video)
                .Where(vp => vp.AttendeeId == attendeeId && vp.Video.EventId == eventId)
                .ToListAsync();
        }

        private async Task CheckAndUpdateCertificateEligibility(int attendeeId, int eventId)
        {
            var isEligible = await CheckCertificateEligibilityAsync(attendeeId, eventId);
            if (isEligible)
            {
                _logger.LogInformation($"Attendee {attendeeId} is now eligible for certificate in event {eventId}");
            }
        }
    }
} 