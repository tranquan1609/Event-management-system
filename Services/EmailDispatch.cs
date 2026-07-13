using Microsoft.Extensions.Configuration;

namespace DACSWEBSK.Services
{
    internal static class EmailDispatch
    {
        public static async Task SendAsync(
            IConfiguration configuration,
            string toEmail,
            string subject,
            string htmlBody)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            var provider = emailSettings["Provider"] ?? "Smtp";
            var fromEmail = emailSettings["SesFromEmail"] ?? emailSettings["SmtpUsername"];

            if (string.IsNullOrEmpty(fromEmail))
            {
                throw new InvalidOperationException("Email settings are not properly configured");
            }

            if (string.Equals(provider, "Ses", StringComparison.OrdinalIgnoreCase))
            {
                var region = configuration["AWS:Region"] ?? "ap-southeast-1";
                await SesEmailClient.SendAsync(region, fromEmail, toEmail, subject, htmlBody);
                return;
            }

            var smtpHost = emailSettings["SmtpHost"];
            var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            var smtpUsername = emailSettings["SmtpUsername"];
            var smtpPassword = emailSettings["SmtpPassword"];

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                throw new InvalidOperationException("Email settings are not properly configured");
            }

            await SesSmtpClient.SendAsync(
                smtpHost,
                smtpPort,
                smtpUsername,
                smtpPassword,
                fromEmail,
                toEmail,
                subject,
                htmlBody);
        }
    }
}
