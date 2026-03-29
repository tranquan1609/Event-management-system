using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DACSWEBSK.Services
{
    public class YoutubeDlService
    {
        private readonly ILogger<YoutubeDlService> _logger;

        public YoutubeDlService(ILogger<YoutubeDlService> logger)
        {
            _logger = logger;
        }

        public string ExtractYouTubeVideoId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var regex = new Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})");
            var match = regex.Match(url);
            return match.Success ? match.Groups[1].Value : null;
        }

        public async Task<string> GetTranscriptAsync(string videoId, string lang = "vi")
        {
            try
            {
                if (string.IsNullOrEmpty(videoId))
                {
                    _logger.LogWarning("Invalid video ID");
                    return null;
                }

                // Tạo thư mục tạm để lưu phụ đề
                var tempDir = Path.Combine(Path.GetTempPath(), "youtube_transcripts");
                Directory.CreateDirectory(tempDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--write-auto-sub --sub-lang {lang} --skip-download -o \"{tempDir}/{videoId}\" \"https://www.youtube.com/watch?v={videoId}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("yt-dlp error: {Error}", error);
                    return null;
                }

                // Tìm file .vtt trong thư mục tạm
                var vttFile = Directory.GetFiles(tempDir, $"{videoId}.{lang}.vtt").FirstOrDefault();

                if (string.IsNullOrEmpty(vttFile))
                {
                    _logger.LogWarning("No .vtt file found for video: {VideoId} in language: {Lang}", videoId, lang);
                    return null;
                }

                // Đọc và chuyển đổi nội dung file .vtt thành text
                var transcript = await ConvertVttToText(vttFile);

                // Xóa file tạm
                try
                {
                    File.Delete(vttFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting temporary file: {File}", vttFile);
                }

                return transcript;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transcript for video: {VideoId}", videoId);
                return null;
            }
        }

        private async Task<string> ConvertVttToText(string vttFile)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(vttFile);
                var textLines = new List<string>();
                var currentText = new StringBuilder();
                var previousLine = string.Empty;

                foreach (var line in lines)
                {
                    // Bỏ qua các dòng header và timestamp
                    if (line.StartsWith("WEBVTT") || 
                        line.Contains("-->") || 
                        string.IsNullOrWhiteSpace(line) ||
                        line.StartsWith("Kind:") ||
                        line.StartsWith("Language:"))
                    {
                        continue;
                    }

                    // Xử lý các thẻ HTML và timestamp
                    var cleanLine = Regex.Replace(line, @"<[^>]+>", ""); // Xóa thẻ HTML
                    cleanLine = Regex.Replace(cleanLine, @"\d{2}:\d{2}:\d{2}\.\d{3}", ""); // Xóa timestamp
                    cleanLine = Regex.Replace(cleanLine, @"\[.*?\]", ""); // Xóa text trong ngoặc vuông
                    cleanLine = Regex.Replace(cleanLine, @"\(.*?\)", ""); // Xóa text trong ngoặc đơn
                    cleanLine = cleanLine.Trim();

                    // Bỏ qua dòng trùng lặp
                    if (!string.IsNullOrWhiteSpace(cleanLine) && cleanLine != previousLine)
                    {
                        currentText.Append(cleanLine).Append(" ");
                        previousLine = cleanLine;
                    }
                }

                var text = currentText.ToString();
                text = Regex.Replace(text, @"\s+", " "); // Xóa khoảng trắng thừa
                text = Regex.Replace(text, @"\s+([.,!?])", @"$1"); // Xóa khoảng trắng trước dấu câu
                text = Regex.Replace(text, @"([.,!?])\s+", @"$1 "); // Thêm khoảng trắng sau dấu câu

                return text.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting VTT to text: {File}", vttFile);
                return null;
            }
        }
    }
} 