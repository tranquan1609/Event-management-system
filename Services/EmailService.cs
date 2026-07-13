using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DACSWEBSK.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendEventApprovalEmailAsync(string to, string fullName, string eventTitle, DateTime eventDate);
        Task SendEventEndedEmailAsync(string recipientEmail, string recipientName, string eventTitle);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task SendEmailAsync(string to, string subject, string body)
        {
            return EmailDispatch.SendAsync(_configuration, to, subject, body);
        }

        public async Task SendEventApprovalEmailAsync(string to, string fullName, string eventTitle, DateTime eventDate)
        {
            var subject = $"Đăng ký tham gia sự kiện {eventTitle} đã được duyệt";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb; margin-bottom: 20px;'>Xác nhận đăng ký tham gia sự kiện</h2>
                        
                        <p>Xin chào <strong>{fullName}</strong>,</p>
                        
                        <p>Chúng tôi xin thông báo rằng đăng ký tham gia sự kiện của bạn đã được phê duyệt:</p>
                        
                        <div style='background-color: #f8fafc; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Sự kiện:</strong> {eventTitle}</p>
                            <p style='margin: 5px 0;'><strong>Thời gian:</strong> {eventDate:dd/MM/yyyy HH:mm}</p>
                        </div>
                        
                        <p>Vui lòng tham gia sự kiện đúng giờ.</p>
                        
                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                            <p style='color: #666; font-size: 14px;'>
                                Trân trọng,<br>
                                Ban tổ chức sự kiện xin chân thành cảm ơn.
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(to, subject, body);
        }

        public async Task SendEventEndedEmailAsync(string recipientEmail, string recipientName, string eventTitle)
        {
            var subject = $"Sự kiện {eventTitle} đã kết thúc";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb; margin-bottom: 20px;'>Thông báo kết thúc sự kiện</h2>
                        
                        <p>Xin chào <strong>{recipientName}</strong>,</p>
                        
                        <p>Sự kiện <strong>{eventTitle}</strong> đã kết thúc. Chúng tôi hy vọng bạn đã có những trải nghiệm tuyệt vời!</p>
                        
                        <p>Cảm ơn bạn đã tham gia sự kiện. Chúng tôi rất mong được gặp lại bạn trong các sự kiện tiếp theo.</p>
                        
                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                            <p style='color: #666; font-size: 14px;'>
                                Trân trọng,<br>
                                Ban tổ chức sự kiện
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(recipientEmail, subject, body);
        }
    }
}