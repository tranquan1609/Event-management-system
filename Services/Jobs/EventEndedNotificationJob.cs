using DACSWEBSK.Models;
using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DACSWEBSK.Services.Jobs
{
    public static class EventEndedNotificationJob
    {
        public static async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var videoProgressService = scope.ServiceProvider.GetRequiredService<IVideoProgressService>();
            var certificateService = scope.ServiceProvider.GetRequiredService<ICertificateService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EventEndedNotificationJob");

            var recentlyEndedEvents = await context.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime <= DateTime.Now &&
                            e.EndTime >= DateTime.Now.AddMinutes(-5))
                .ToListAsync(cancellationToken);

            foreach (var @event in recentlyEndedEvents)
            {
                logger.LogInformation("Sending ended notifications for event: {Title}", @event.Title);

                foreach (var attendee in @event.Attendees)
                {
                    try
                    {
                        var isEligible = await videoProgressService.CheckCertificateEligibilityAsync(
                            attendee.Id, @event.Id);

                        if (isEligible)
                        {
                            var certificate = await certificateService.GenerateCertificateAsync(attendee, @event);
                            await SendEventEndedEmailWithCertificateAsync(
                                emailService, attendee, @event, certificate);
                        }
                        else
                        {
                            await SendEventEndedEmailAsync(emailService, attendee, @event);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Failed to send notification to {Email} for event {Title}",
                            attendee.Email,
                            @event.Title);
                    }
                }
            }
        }

        private static Task SendEventEndedEmailWithCertificateAsync(
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
                            <p style='color: #0369a1; font-weight: bold;'>Chúc mừng!</p>
                            <p>Bạn đã hoàn thành các yêu cầu và nhận được chứng nhận tham gia.</p>
                            <p>Mã chứng nhận: {certificate.CertificateNumber}</p>
                            <p><a href='{certificate.CertificateUrl}' style='color: #0284c7;'>Tải chứng nhận</a></p>
                        </div>
                    </div>
                </body>
                </html>";

            return emailService.SendEmailAsync(attendee.Email, subject, body);
        }

        private static Task SendEventEndedEmailAsync(
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
                            <p style='color: #9a3412; font-weight: bold;'>Lưu ý</p>
                            <p>Bạn chưa đủ điều kiện để nhận chứng nhận tham gia.</p>
                        </div>
                    </div>
                </body>
                </html>";

            return emailService.SendEmailAsync(attendee.Email, subject, body);
        }
    }
}
