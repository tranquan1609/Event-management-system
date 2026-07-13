using DACSWEBSK.Models;
using DACSWEBSK.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
using QRCoder;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Drawing.Text;

namespace DACSWEBSK.Services
{
    public class CertificateService : ICertificateService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<CertificateService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly IVideoProgressService _videoProgressService;
        private readonly IFileStorageService _fileStorage;

        public CertificateService(
            ApplicationDbContext context,
            IEmailService emailService,
            IWebHostEnvironment webHostEnvironment,
            ILogger<CertificateService> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IVideoProgressService videoProgressService,
            IFileStorageService fileStorage)
        {
            _context = context;
            _emailService = emailService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _videoProgressService = videoProgressService;
            _fileStorage = fileStorage;

            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            if (_fileStorage.IsS3)
            {
                return;
            }

            string certificatesDir = Path.Combine(_webHostEnvironment.WebRootPath, "certificates");
            if (!Directory.Exists(certificatesDir))
            {
                Directory.CreateDirectory(certificatesDir);
            }

            string imagesDir = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            if (!Directory.Exists(imagesDir))
            {
                Directory.CreateDirectory(imagesDir);
            }
        }

        private string GetBaseUrl()
        {
            // Ưu tiên lấy từ configuration
            string baseUrl = _configuration["AppSettings:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                return baseUrl.TrimEnd('/');
            }

            // Nếu không có trong configuration, thử lấy từ HttpContext
            if (_httpContextAccessor.HttpContext != null)
            {
                var request = _httpContextAccessor.HttpContext.Request;
                return $"{request.Scheme}://{request.Host}".TrimEnd('/');
            }

            // Fallback to default URL (có thể cấu hình trong appsettings.json)
            return "https://localhost:7080".TrimEnd('/');
        }

        private string GetCertificateUrl(string path)
        {
            string baseUrl = GetBaseUrl(); // e.g., "https://localhost:7080"

            // Sanitize the path to ensure it's just the relative part like "/certificates/filename.png"
            string cleanedPath = path;

            // Remove common incorrect prefixes if they exist in the path itself
            // This handles cases like "http://certificates/...", "https://localhost:7080/certificates/..."
            if (cleanedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                cleanedPath = cleanedPath.Substring("http://".Length);
            }
            if (cleanedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleanedPath = cleanedPath.Substring("https://".Length);
            }
            // Remove the base URL itself if it's accidentally part of the path
            if (cleanedPath.StartsWith(baseUrl.Replace("http://", "").Replace("https://", "").TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                cleanedPath = cleanedPath.Substring(baseUrl.Replace("http://", "").Replace("https://", "").TrimEnd('/').Length);
            }
            // Ensure it starts with a single slash, removing multiple leading slashes
            cleanedPath = "/" + cleanedPath.TrimStart('/');

            // Now, combine the base URL and the cleaned relative path using Uri constructor
            try
            {
                // Uri constructor with two strings is generally safe for base and relative parts
                Uri combinedUri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), cleanedPath);
                return combinedUri.ToString();
            }
            catch (UriFormatException ex)
            {
                // Log the error for debugging
                _logger.LogError(ex, $"Failed to combine base URL '{baseUrl}' with cleaned path '{cleanedPath}'. Original path: '{path}'. Falling back to simple concatenation.");
                // Fallback to simple string concatenation as a last resort
                return baseUrl.TrimEnd('/') + cleanedPath;
            }
        }

        public async Task<Certificate> GenerateCertificateAsync(Attendee attendee, Event @event)
        {
            // Kiểm tra điều kiện cấp chứng nhận
            var isEligible = await _videoProgressService.CheckCertificateEligibilityAsync(attendee.Id, @event.Id);
            if (!isEligible)
            {
                throw new InvalidOperationException("Người tham gia chưa đủ điều kiện nhận chứng nhận");
            }

            // Generate unique certificate number
            string certificateNumber = GenerateUniqueCertificateNumber();

            // Create certificate record
            var certificate = new Certificate
            {
                CertificateNumber = certificateNumber,
                AttendeeId = attendee.Id,
                EventId = @event.Id,
                IssueDate = DateTime.Now
            };

            try
            {
                string storedPath = await GenerateCertificateImageAsync(certificate, attendee, @event);
                certificate.CertificateUrl = storedPath;

                // Save to database
                await _context.Certificates.AddAsync(certificate);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã tạo chứng nhận thành công cho {attendee.FullName} - Sự kiện: {@event.Title}");
                return certificate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi tạo chứng nhận cho {attendee.FullName} - Sự kiện: {@event.Title}");
                throw;
            }
        }

        public async Task<bool> SendCertificateEmailAsync(Certificate certificate)
        {
            try
            {
                var attendee = await _context.Attendees
                    .Include(a => a.Event)
                    .FirstOrDefaultAsync(a => a.Id == certificate.AttendeeId);

                if (attendee == null) return false;

                string certificateUrl = _fileStorage.GetAccessUrl(certificate.CertificateUrl);
                if (string.IsNullOrEmpty(certificateUrl))
                {
                    certificateUrl = GetCertificateUrl(certificate.CertificateUrl);
                }

                string emailBody = $@"
                    <h2>Xin chúc mừng {attendee.FullName}!</h2>
                    <p>Bạn đã hoàn thành tiến độ tham gia sự kiện '{attendee.Event.Title}'.</p>
                    <p>Chứng chỉ của bạn đã được cấp. Vui lòng tải về từ đường dẫn bên dưới:</p>
                    <p><a href='{certificateUrl}'>Tải chứng chỉ</a></p>
                    <p>Mã chứng chỉ: {certificate.CertificateNumber}</p>
                    <p>Bạn có thể xác thực chứng chỉ này trên website của chúng tôi.</p>";

                await _emailService.SendEmailAsync(
                    attendee.Email,
                    $"Chứng chỉ tham gia sự kiện - {attendee.Event.Title}",
                    emailBody);

                certificate.IsEmailSent = true;
                certificate.EmailSentDate = DateTime.Now;
                _context.Certificates.Update(certificate);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gửi email chứng nhận: {ex.Message}");
                return false;
            }
        }

        private async Task<string> GenerateCertificateImageAsync(Certificate certificate, Attendee attendee, Event @event)
        {
            using var image = await LoadTemplateImageAsync();
            using var graphics = Graphics.FromImage(image);

            // Configure text formatting with better fonts and colors
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Define colors
            Color primaryColor = ColorTranslator.FromHtml("#0077b6"); // Dark blue
            Color secondaryColor = ColorTranslator.FromHtml("#03045e"); // Even darker blue
            Color textColor = Color.Black; // For other text

            // Define fonts
            using var programFont = new Font("Segoe UI", 28, FontStyle.Bold); // Slightly larger for "Chương trình"
            using var eventAttendeeFont = new Font("Segoe UI", 32, FontStyle.Bold); // Slightly smaller than 36 for better fit
            using var detailFont = new Font("Segoe UI", 22); // For cert number and issue date

            // Create brushes
            using var primaryBrush = new SolidBrush(primaryColor);
            using var secondaryBrush = new SolidBrush(secondaryColor);
            using var textBrush = new SolidBrush(textColor);

            // Calculate center points for text
            float centerX = image.Width / 2f;
            float leftMargin = 150f; // Lề trái cho QR code và chữ ký
            float rightMargin = 150f; // Lề phải

            // Draw "CHỨNG NHẬN" - chỉ một dòng, không có "VẬN"
            string certificateTitle = "CHỨNG NHẬN";
            using var titleFont = new Font("Times New Roman", 72, FontStyle.Bold);
            var certificateTitleSize = graphics.MeasureString(certificateTitle, titleFont);
            graphics.DrawString(certificateTitle, titleFont, secondaryBrush, 
                                centerX - (certificateTitleSize.Width / 2), 150);

            // Draw "Chương trình" (Program)
            string programText = "Chương trình";
            var programTextSize = graphics.MeasureString(programText, programFont);
            graphics.DrawString(programText, programFont, textBrush,
                                centerX - (programTextSize.Width / 2), 280);

            // Draw Event Title
            string eventTitle = @event.Title;
            var eventTitleSize = graphics.MeasureString(eventTitle, eventAttendeeFont);
            // Nếu tên sự kiện quá dài, chia thành nhiều dòng
            if (eventTitleSize.Width > image.Width - 300)
            {
                // Chia thành 2 dòng
                int maxCharsPerLine = (int)((image.Width - 300) / (eventTitleSize.Width / eventTitle.Length));
                if (eventTitle.Length > maxCharsPerLine)
                {
                    int splitPoint = eventTitle.LastIndexOf(' ', maxCharsPerLine);
                    if (splitPoint == -1) splitPoint = maxCharsPerLine;
                    string line1 = eventTitle.Substring(0, splitPoint);
                    string line2 = eventTitle.Substring(splitPoint).Trim();
                    var line1Size = graphics.MeasureString(line1, eventAttendeeFont);
                    var line2Size = graphics.MeasureString(line2, eventAttendeeFont);
                    graphics.DrawString(line1, eventAttendeeFont, primaryBrush,
                                        centerX - (line1Size.Width / 2), 320);
                    graphics.DrawString(line2, eventAttendeeFont, primaryBrush,
                                        centerX - (line2Size.Width / 2), 360);
                }
                else
                {
                    graphics.DrawString(eventTitle, eventAttendeeFont, primaryBrush,
                                        centerX - (eventTitleSize.Width / 2), 320);
                }
            }
            else
            {
                graphics.DrawString(eventTitle, eventAttendeeFont, primaryBrush,
                                    centerX - (eventTitleSize.Width / 2), 320);
            }

            // Draw "Đã hoàn thành" text
            using var completedFont = new Font("Segoe UI", 24, FontStyle.Italic);
            string completedText = "đã hoàn thành";
            var completedTextSize = graphics.MeasureString(completedText, completedFont);
            graphics.DrawString(completedText, completedFont, textBrush,
                                centerX - (completedTextSize.Width / 2), 420);

            // Draw Attendee Name
            string attendeeName = attendee.FullName;
            var attendeeNameSize = graphics.MeasureString(attendeeName, eventAttendeeFont);
            graphics.DrawString(attendeeName, eventAttendeeFont, primaryBrush,
                                centerX - (attendeeNameSize.Width / 2), 480);

            // Draw Certificate Number - chỉ một dòng, không lặp lại
            string certNumberText = $"Mã chứng nhận: {certificate.CertificateNumber}";
            var certNumberSize = graphics.MeasureString(certNumberText, detailFont);
            graphics.DrawString(certNumberText, detailFont, textBrush,
                                centerX - (certNumberSize.Width / 2), 580);

            // Draw Issue Date
            string issueDateText = $"Ngày cấp: {certificate.IssueDate:dd/MM/yyyy}";
            var issueDateSize = graphics.MeasureString(issueDateText, detailFont);
            graphics.DrawString(issueDateText, detailFont, textBrush,
                                centerX - (issueDateSize.Width / 2), 620);

            string fileName = $"certificate-{certificate.CertificateNumber}.png";
            string qrCodeAbsoluteUrl = $"{GetBaseUrl()}/Admin/Certificate/Verify";

            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(
                qrCodeAbsoluteUrl,
                QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new BitmapByteQRCode(qrCodeData);
            using var qrCodeImage = new Bitmap(new MemoryStream(qrCode.GetGraphic(10))); 
            
            // Đặt QR code ở góc dưới bên trái
            float qrCodeX = leftMargin;
            float qrCodeY = image.Height - qrCodeImage.Height - 120;
            graphics.DrawImage(qrCodeImage, new PointF(qrCodeX, qrCodeY));

            // Vẽ nhãn cho QR code
            using var qrLabelFont = new Font("Segoe UI", 14);
            string qrLabel = "Quét để xác thực";
            var qrLabelSize = graphics.MeasureString(qrLabel, qrLabelFont);
            graphics.DrawString(qrLabel, qrLabelFont, textBrush,
                                qrCodeX + (qrCodeImage.Width / 2) - (qrLabelSize.Width / 2),
                                qrCodeY + qrCodeImage.Height + 10);

            // Vẽ khung chữ ký/đóng dấu ở góc dưới bên phải
            float signatureBoxSize = 200;
            float signatureBoxX = image.Width - rightMargin - signatureBoxSize;
            float signatureBoxY = image.Height - 200;
            
            using var signaturePen = new Pen(Color.FromArgb(100, 100, 100), 2);
            graphics.DrawRectangle(signaturePen, signatureBoxX, signatureBoxY, signatureBoxSize, 80);
            
            // Vẽ nhãn "Chữ ký/Đóng dấu"
            using var signatureLabelFont = new Font("Segoe UI", 12);
            string signatureLabel = "Chữ ký/Đóng dấu";
            var signatureLabelSize = graphics.MeasureString(signatureLabel, signatureLabelFont);
            graphics.DrawString(signatureLabel, signatureLabelFont, textBrush,
                                signatureBoxX + (signatureBoxSize / 2) - (signatureLabelSize.Width / 2),
                                signatureBoxY - 25);

            await using var outputStream = new MemoryStream();
            image.Save(outputStream, ImageFormat.Png);
            var bytes = outputStream.ToArray();

            return await _fileStorage.UploadBytesAsync(
                bytes,
                StorageCategory.Certificate,
                fileName,
                "image/png");
        }

        private async Task<Image> LoadTemplateImageAsync()
        {
            var candidates = new List<string> { "/images/certificate-template.png" };

            var imagesBucket = _configuration["S3:ImagesBucket"];
            if (!string.IsNullOrWhiteSpace(imagesBucket))
            {
                candidates.Add($"s3:{imagesBucket}/templates/certificate-template.png");
            }

            foreach (var candidate in candidates)
            {
                var opened = await _fileStorage.OpenReadAsync(candidate);
                if (opened != null)
                {
                    return Image.FromStream(opened.Value.Stream);
                }
            }

            string localPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "certificate-template.png");
            if (File.Exists(localPath))
            {
                return Image.FromFile(localPath);
            }

            throw new FileNotFoundException(
                "Template chứng nhận không tồn tại. Đặt file tại wwwroot/images/certificate-template.png hoặc upload lên S3: images bucket / templates/certificate-template.png");
        }

        private string GenerateUniqueCertificateNumber()
        {
            return $"CERT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        public async Task<IEnumerable<Certificate>> GetCertificatesByEventAsync(int eventId)
        {
            return await _context.Certificates
                .Include(c => c.Attendee)
                .Include(c => c.Event)
                .Where(c => c.EventId == eventId)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Certificate>> GetCertificatesByAttendeeEmailAsync(string email)
        {
            return await _context.Certificates
                .Include(c => c.Attendee)
                .Include(c => c.Event)
                .Where(c => c.Attendee.Email == email)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();
        }

        public async Task<bool> VerifyCertificateAsync(string certificateNumber)
        {
            var certificate = await _context.Certificates
                .Include(c => c.Attendee)
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber);

            return certificate != null;
        }
    }
} 