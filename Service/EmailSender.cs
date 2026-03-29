using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace DACSWEBSK.Services
{
    public class EmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpClient = new SmtpClient(_config["EmailSettings:SmtpServer"])
                {
                    Port = int.Parse(_config["EmailSettings:SmtpPort"]),
                    Credentials = new NetworkCredential(
                        _config["EmailSettings:SmtpUser"],
                        _config["EmailSettings:SmtpPass"]
                    ),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_config["EmailSettings:SmtpUser"], "Event Online"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi hoặc throw lại để xử lý ở nơi gọi
                throw new InvalidOperationException("Gửi email thất bại: " + ex.Message, ex);
            }
        }
    }
}
