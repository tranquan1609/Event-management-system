using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DACSWEBSK.Models;
using DACSWEBSK.Data;

namespace DACSWEBSK.Services
{
    public class AutoCertificateService : BackgroundService
    {
        private readonly ILogger<AutoCertificateService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30); // Kiểm tra mỗi 30 giây

        public AutoCertificateService(
            ILogger<AutoCertificateService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEndedEvents();
                    await RecheckPendingCertificates();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý cấp chứng nhận tự động");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessEndedEvents()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var certificateService = scope.ServiceProvider.GetRequiredService<ICertificateService>();
            var videoProgressService = scope.ServiceProvider.GetRequiredService<IVideoProgressService>();

            // Lấy các sự kiện vừa kết thúc (trong vòng 1 phút)
            var recentlyEndedEvents = await context.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime <= DateTime.Now && 
                          e.EndTime >= DateTime.Now.AddSeconds(-30)) // Kiểm tra trong 30 giây gần nhất
                .ToListAsync();

            foreach (var @event in recentlyEndedEvents)
            {
                _logger.LogInformation($"Đang xử lý chứng nhận cho sự kiện: {@event.Title}");

                foreach (var attendee in @event.Attendees)
                {
                    try
                    {
                        await ProcessAttendee(attendee, @event, videoProgressService, certificateService, context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Lỗi khi xử lý chứng nhận cho {attendee.Email} trong sự kiện {@event.Title}");
                    }
                }
            }
        }

        private async Task RecheckPendingCertificates()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var certificateService = scope.ServiceProvider.GetRequiredService<ICertificateService>();
            var videoProgressService = scope.ServiceProvider.GetRequiredService<IVideoProgressService>();

            // Lấy các sự kiện đã kết thúc nhưng có người tham gia chưa nhận chứng nhận
            var endedEvents = await context.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime <= DateTime.Now &&
                           e.Attendees.Any(a => a.IsEligibleForCertificate &&
                                              !context.Certificates.Any(c => c.AttendeeId == a.Id && c.EventId == e.Id)))
                .ToListAsync();

            foreach (var @event in endedEvents)
            {
                _logger.LogInformation($"Đang kiểm tra lại chứng nhận cho sự kiện: {@event.Title}");

                var eligibleAttendees = @event.Attendees
                    .Where(a => a.IsEligibleForCertificate &&
                               !context.Certificates.Any(c => c.AttendeeId == a.Id && c.EventId == @event.Id));

                foreach (var attendee in eligibleAttendees)
                {
                    try
                    {
                        await ProcessAttendee(attendee, @event, videoProgressService, certificateService, context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Lỗi khi xử lý chứng nhận cho {attendee.Email} trong sự kiện {@event.Title}");
                    }
                }
            }
        }

        private async Task ProcessAttendee(
            Attendee attendee, 
            Event @event, 
            IVideoProgressService videoProgressService,
            ICertificateService certificateService,
            ApplicationDbContext context)
        {
            // Kiểm tra điều kiện nhận chứng nhận
            var isEligible = await videoProgressService.CheckCertificateEligibilityAsync(
                attendee.Id, @event.Id);

            if (isEligible)
            {
                // Kiểm tra xem đã có chứng nhận chưa
                var existingCertificate = await context.Certificates
                    .FirstOrDefaultAsync(c => c.AttendeeId == attendee.Id && 
                                            c.EventId == @event.Id);

                if (existingCertificate == null)
                {
                    try
                    {
                        // Tạo và gửi chứng nhận
                        var certificate = await certificateService.GenerateCertificateAsync(
                            attendee, @event);
                        
                        var emailSent = await certificateService.SendCertificateEmailAsync(certificate);
                        if (emailSent)
                        {
                            _logger.LogInformation(
                                $"Đã gửi chứng nhận cho {attendee.Email} - Sự kiện: {@event.Title}");
                        }
                        else
                        {
                            _logger.LogWarning(
                                $"Không thể gửi email chứng nhận cho {attendee.Email} - Sự kiện: {@event.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            $"Lỗi khi tạo/gửi chứng nhận cho {attendee.Email} - Sự kiện: {@event.Title}");
                        throw;
                    }
                }
            }
        }
    }
} 