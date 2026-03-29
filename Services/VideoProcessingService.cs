using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace DACSWEBSK.Services
{
    public class VideoProcessingService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly OpenAIService _openAIService;

        public VideoProcessingService(IWebHostEnvironment webHostEnvironment, OpenAIService openAIService)
        {
            _webHostEnvironment = webHostEnvironment;
            _openAIService = openAIService;
        }

        // Trích xuất frame đầu tiên sau 5 giây
        public string ExtractFrame(string videoPath)
        {
            var ffmpegPath = Path.Combine(_webHostEnvironment.WebRootPath, "ffmpeg", "ffmpeg.exe");
            var outputDir = Path.Combine(_webHostEnvironment.WebRootPath, "temp_frames");
            Directory.CreateDirectory(outputDir);

            var outputImagePath = Path.Combine(outputDir, $"frame_{Guid.NewGuid()}.jpg");
            var args = $"-ss 00:00:05 -i \"{videoPath}\" -frames:v 1 \"{outputImagePath}\" -y";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();

            // Kiểm tra file có tồn tại không
            return File.Exists(outputImagePath) ? outputImagePath : null;
        }

        // Lấy metadata video
        public string GetVideoInfo(string videoPath)
        {
            var ffmpegPath = Path.Combine(_webHostEnvironment.WebRootPath, "ffmpeg", "ffmpeg.exe");
            var args = $"-i \"{videoPath}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        // Tạo tóm tắt tự động
        public async Task<string> GenerateVideoSummary(string videoPath)
        {
            // 1. Lấy metadata
            var info = GetVideoInfo(videoPath);

            // 2. Trích xuất frame
            var framePath = ExtractFrame(videoPath);

            // 3. Mô tả frame (nếu có)
            string frameDescription = "";
            if (!string.IsNullOrEmpty(framePath))
            {
                var prompt = $"Hãy mô tả ngắn gọn nội dung của hình ảnh này trong 1-2 câu: {framePath}";
                frameDescription = await _openAIService.GenerateText(prompt);
            }

            // 4. Tạo prompt tổng hợp
            var summaryPrompt = new StringBuilder();
            summaryPrompt.AppendLine("Dựa trên thông tin video sau, hãy tạo một tóm tắt ngắn gọn trong khoảng 100-150 từ:");
            summaryPrompt.AppendLine(info);
            if (!string.IsNullOrEmpty(frameDescription))
            {
                summaryPrompt.AppendLine("Mô tả cảnh chính:");
                summaryPrompt.AppendLine(frameDescription);
            }
            summaryPrompt.AppendLine("Hãy tóm tắt nội dung chính của video, các điểm quan trọng và thông điệp chính.");

            // 5. Gọi OpenAI để tạo tóm tắt
            return await _openAIService.GenerateText(summaryPrompt.ToString());
        }
    }
} 