using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DACSWEBSK.Models;

namespace DACSWEBSK.Services
{
    public class EventEndedEmailService : BackgroundService
    {
        private readonly ILogger<EventEndedEmailService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Kiểm tra mỗi phút

        public EventEndedEmailService(
            ILogger<EventEndedEmailService> logger,
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gửi email thông báo kết thúc sự kiện");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessEndedEvents()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var videoProgressService = scope.ServiceProvider.GetRequiredService<IVideoProgressService>();
            var certificateService = scope.ServiceProvider.GetRequiredService<ICertificateService>();

            // Lấy các sự kiện vừa kết thúc (trong vòng 1 phút)
            var recentlyEndedEvents = await context.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime <= DateTime.Now && 
                          e.EndTime >= DateTime.Now.AddMinutes(-1))
                .ToListAsync();

            foreach (var @event in recentlyEndedEvents)
            {
                _logger.LogInformation($"Đang gửi email thông báo kết thúc cho sự kiện: {@event.Title}");

                foreach (var attendee in @event.Attendees)
                {
                    try
                    {
                        // Kiểm tra điều kiện nhận chứng nhận
                        var isEligible = await videoProgressService.CheckCertificateEligibilityAsync(
                            attendee.Id, @event.Id);

                        if (isEligible)
                        {
                            // Tạo chứng nhận nếu đủ điều kiện
                            var certificate = await certificateService.GenerateCertificateAsync(
                                attendee, @event);

                            // Gửi email kèm chứng nhận
                            await SendEventEndedEmailWithCertificateAsync(
                                emailService, attendee, @event, certificate);
                        }
                        else
                        {
                            // Gửi email thông báo kết thúc bình thường
                            await SendEventEndedEmailAsync(
                                emailService, attendee, @event);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            $"Lỗi khi gửi email cho {attendee.Email} - Sự kiện: {@event.Title}");
                    }
                }
            }
        }

        private async Task SendEventEndedEmailWithCertificateAsync(
            IEmailService emailService,
            Attendee attendee,
            Event @event,
            Certificate certificate)
        {
            var subject = $"Sự kiện {@event.Title} đã kết thúc - Chứng nhận tham gia";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb; margin-bottom: 20px;'>Thông báo kết thúc sự kiện</h2>
                        
                        <p>Xin chào <strong>{attendee.FullName}</strong>,</p>
                        
                        <p>Sự kiện <strong>{@event.Title}</strong> đã kết thúc. Cảm ơn bạn đã tham gia!</p>
                        
                        <div style='background-color: #f0f9ff; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='color: #0369a1; font-weight: bold;'>🎉 Chúc mừng!</p>
                            <p>Bạn đã hoàn thành tất cả các yêu cầu và nhận được chứng nhận tham gia.</p>
                            <p>Vui lòng xem chứng nhận đính kèm trong email này.</p>
                            <p>Mã chứng nhận: {certificate.CertificateNumber}</p>
                            <p><a href='{certificate.CertificateUrl}' style='color: #0284c7;'>Tải chứng nhận</a></p>
                        </div>
                        
                        <p>Chúng tôi rất mong được gặp lại bạn trong các sự kiện tiếp theo.</p>
                        
                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                            <p style='color: #666; font-size: 14px;'>
                                Trân trọng,<br>
                                Ban tổ chức sự kiện
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            await emailService.SendEmailAsync(attendee.Email, subject, body);
            _logger.LogInformation(
                $"Đã gửi email kết thúc kèm chứng nhận cho {attendee.Email} - Sự kiện: {@event.Title}");
        }

        private async Task SendEventEndedEmailAsync(
            IEmailService emailService,
            Attendee attendee,
            Event @event)
        {
            var subject = $"Sự kiện {@event.Title} đã kết thúc";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb; margin-bottom: 20px;'>Thông báo kết thúc sự kiện</h2>
                        
                        <p>Xin chào <strong>{attendee.FullName}</strong>,</p>
                        
                        <p>Sự kiện <strong>{@event.Title}</strong> đã kết thúc. Cảm ơn bạn đã tham gia!</p>
                        
                        <div style='background-color: #fff7ed; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='color: #9a3412; font-weight: bold;'>ℹ️ Lưu ý</p>
                            <p>Bạn chưa đủ điều kiện để nhận chứng nhận tham gia.</p>
                            <p>Vui lòng hoàn thành tất cả các video bắt buộc để nhận chứng nhận.</p>
                        </div>
                        
                        <p>Chúng tôi rất mong được gặp lại bạn trong các sự kiện tiếp theo.</p>
                        
                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                            <p style='color: #666; font-size: 14px;'>
                                Trân trọng,<br>
                                Ban tổ chức sự kiện
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            await emailService.SendEmailAsync(attendee.Email, subject, body);
            _logger.LogInformation(
                $"Đã gửi email kết thúc cho {attendee.Email} - Sự kiện: {@event.Title}");
        }
    }
} 