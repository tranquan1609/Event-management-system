namespace DACSWEBSK.Services.Storage
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public LocalFileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public bool IsS3 => false;

        public async Task<string> UploadAsync(IFormFile file, StorageCategory category)
        {
            var (folder, webPrefix) = GetFolderMapping(category);
            var uploadsFolder = Path.Combine(_environment.WebRootPath, folder);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"{webPrefix}/{uniqueFileName}";
        }

        public async Task<string> UploadBytesAsync(byte[] data, StorageCategory category, string fileName, string contentType)
        {
            var (folder, webPrefix) = GetFolderMapping(category);
            var uploadsFolder = Path.Combine(_environment.WebRootPath, folder);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            await File.WriteAllBytesAsync(filePath, data);

            return $"{webPrefix}/{uniqueFileName}";
        }

        public Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(string storedPath)
        {
            var physicalPath = GetPhysicalPath(storedPath);
            if (!File.Exists(physicalPath))
            {
                return Task.FromResult<(Stream, string, string)?>(null);
            }

            var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var contentType = GetContentType(physicalPath);
            return Task.FromResult<(Stream, string, string)?>((stream, contentType, Path.GetFileName(physicalPath)));
        }

        public Task DeleteAsync(string storedPath)
        {
            var physicalPath = GetPhysicalPath(storedPath);
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            return Task.CompletedTask;
        }

        public string GetAccessUrl(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return string.Empty;
            }

            if (storedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                storedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return storedPath;
            }

            return storedPath.StartsWith('/') ? storedPath : "/" + storedPath.TrimStart('/');
        }

        public string? GetLocalPathOrNull(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath) || storedPath.StartsWith("s3:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return GetPhysicalPath(storedPath);
        }

        private string GetPhysicalPath(string storedPath)
        {
            var relative = storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_environment.WebRootPath, relative);
        }

        private static (string Folder, string WebPrefix) GetFolderMapping(StorageCategory category)
        {
            return category switch
            {
                StorageCategory.EventImage => ("images/events", "/images/events"),
                StorageCategory.GiftImage => ("images/gifts", "/images/gifts"),
                StorageCategory.Video => ("videos", "/videos"),
                StorageCategory.Certificate => ("certificates", "/certificates"),
                StorageCategory.Assignment => ("uploads/assignments", "/uploads/assignments"),
                StorageCategory.Evidence => ("uploads/evidence", "/uploads/evidence"),
                StorageCategory.CertificateTemplate => ("images", "/images"),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".mp4" => "video/mp4",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
    }
}
