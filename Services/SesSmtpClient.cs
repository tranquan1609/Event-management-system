using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DACSWEBSK.Services
{
    internal static class SesSmtpClient
    {
        public static async Task SendAsync(
            string smtpHost,
            int smtpPort,
            string smtpUsername,
            string smtpPassword,
            string fromEmail,
            string toEmail,
            string subject,
            string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUsername, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
