using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DACSWEBSK.Services
{
    public class CertificateTemplateService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CertificateTemplateService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public void GenerateDefaultTemplate()
        {
            string imagesDir = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            Directory.CreateDirectory(imagesDir);

            string templatePath = Path.Combine(imagesDir, "certificate-template.png");

            // Tạo một hình ảnh mới với kích thước A4 landscape
            using var bitmap = new Bitmap(2480, 1748); // A4 size at 300 DPI
            using var graphics = Graphics.FromImage(bitmap);

            // Thiết lập chất lượng vẽ cao
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Vẽ nền
            using var backgroundBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(bitmap.Width, bitmap.Height),
                Color.FromArgb(255, 252, 244), // Màu kem nhạt
                Color.FromArgb(255, 248, 230)  // Màu kem đậm hơn
            );
            graphics.FillRectangle(backgroundBrush, 0, 0, bitmap.Width, bitmap.Height);

            // Vẽ viền ngoài
            int borderMargin = 50;
            using var outerBorderPen = new Pen(Color.FromArgb(180, 151, 90), 3); // Màu vàng đồng
            graphics.DrawRectangle(outerBorderPen,
                borderMargin, borderMargin,
                bitmap.Width - (borderMargin * 2),
                bitmap.Height - (borderMargin * 2));

            // Vẽ viền trong
            int innerBorderMargin = 70;
            using var innerBorderPen = new Pen(Color.FromArgb(180, 151, 90), 1);
            graphics.DrawRectangle(innerBorderPen,
                innerBorderMargin, innerBorderMargin,
                bitmap.Width - (innerBorderMargin * 2),
                bitmap.Height - (innerBorderMargin * 2));

            // Vẽ hoa văn góc
            DrawCornerOrnament(graphics, 50, 50, 200, 200); // Góc trên trái
            DrawCornerOrnament(graphics, bitmap.Width - 250, 50, 200, 200); // Góc trên phải
            DrawCornerOrnament(graphics, 50, bitmap.Height - 250, 200, 200); // Góc dưới trái
            DrawCornerOrnament(graphics, bitmap.Width - 250, bitmap.Height - 250, 200, 200); // Góc dưới phải

            // Vẽ tiêu đề "CHỨNG NHẬN" - chỉ một dòng, không có "VẬN"
            using var titleFont = new Font("Times New Roman", 80, FontStyle.Bold);
            var titleText = "CHỨNG NHẬN";
            var titleSize = graphics.MeasureString(titleText, titleFont);
            graphics.DrawString(titleText, titleFont,
                new SolidBrush(Color.FromArgb(25, 25, 112)), // Màu xanh đậm
                (bitmap.Width - titleSize.Width) / 2,
                180);

            // Lưu template
            bitmap.Save(templatePath, System.Drawing.Imaging.ImageFormat.Png);
        }

        private void DrawCornerOrnament(Graphics g, float x, float y, float width, float height)
        {
            // Vẽ hoa văn góc
            using var ornamentPen = new Pen(Color.FromArgb(180, 151, 90), 2);
            
            // Vẽ đường cong trang trí
            var path = new GraphicsPath();
            path.AddArc(x, y, width/2, height/2, 180, 90);
            path.AddArc(x + width/4, y + height/4, width/4, height/4, 180, -180);
            g.DrawPath(ornamentPen, path);

            // Vẽ chi tiết phụ
            g.DrawEllipse(ornamentPen, x + width/4, y + height/4, width/8, height/8);
            g.DrawEllipse(ornamentPen, x + width/2, y + height/2, width/8, height/8);
        }
    }
} 