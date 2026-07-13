using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

namespace DACSWEBSK.Services
{
    internal static class SesEmailClient
    {
        public static async Task SendAsync(
            string region,
            string fromEmail,
            string toEmail,
            string subject,
            string htmlBody)
        {
            var request = new SendEmailRequest
            {
                Source = fromEmail,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { toEmail }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = htmlBody
                        }
                    }
                }
            };

            using var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.GetBySystemName(region));
            await client.SendEmailAsync(request);
        }
    }
}
