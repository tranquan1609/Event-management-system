using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace DACSWEBSK.Services.Storage
{
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3;
        private readonly S3StorageOptions _options;
        private readonly ILogger<S3FileStorageService> _logger;

        public S3FileStorageService(
            IAmazonS3 s3,
            IOptions<S3StorageOptions> options,
            ILogger<S3FileStorageService> logger)
        {
            _s3 = s3;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsS3 => true;

        public async Task<string> UploadAsync(IFormFile file, StorageCategory category)
        {
            var bucket = GetBucket(category);
            var key = BuildObjectKey(category, file.FileName);

            await using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType ?? GetContentType(file.FileName)
            };

            await _s3.PutObjectAsync(request);
            _logger.LogInformation("Uploaded {Key} to s3://{Bucket}", key, bucket);

            return FormatStoredPath(bucket, key);
        }

        public async Task<string> UploadBytesAsync(byte[] data, StorageCategory category, string fileName, string contentType)
        {
            var bucket = GetBucket(category);
            var key = BuildObjectKey(category, fileName);

            await using var stream = new MemoryStream(data);
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType
            };

            await _s3.PutObjectAsync(request);
            return FormatStoredPath(bucket, key);
        }

        public async Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(string storedPath)
        {
            if (!TryParseStoredPath(storedPath, out var bucket, out var key))
            {
                return null;
            }

            try
            {
                var response = await _s3.GetObjectAsync(bucket, key);
                return (response.ResponseStream, response.Headers.ContentType ?? GetContentType(key), Path.GetFileName(key));
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("S3 object not found: s3://{Bucket}/{Key}", bucket, key);
                return null;
            }
        }

        public async Task DeleteAsync(string storedPath)
        {
            if (!TryParseStoredPath(storedPath, out var bucket, out var key))
            {
                return;
            }

            await _s3.DeleteObjectAsync(bucket, key);
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

            if (!storedPath.StartsWith("s3:", StringComparison.OrdinalIgnoreCase))
            {
                return storedPath.StartsWith('/') ? storedPath : "/" + storedPath.TrimStart('/');
            }

            if (!TryParseStoredPath(storedPath, out var bucket, out var key))
            {
                return storedPath;
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(_options.PresignedUrlExpiryMinutes)
            };

            return _s3.GetPreSignedURL(request);
        }

        public string? GetLocalPathOrNull(string storedPath) => null;

        private string GetBucket(StorageCategory category)
        {
            var bucket = category switch
            {
                StorageCategory.EventImage => _options.ImagesBucket,
                StorageCategory.GiftImage => _options.ImagesBucket,
                StorageCategory.CertificateTemplate => _options.ImagesBucket,
                StorageCategory.Video => _options.VideosBucket,
                StorageCategory.Certificate => _options.CertificatesBucket,
                StorageCategory.Assignment => _options.UploadsBucket,
                StorageCategory.Evidence => _options.UploadsBucket,
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(bucket))
            {
                throw new InvalidOperationException($"S3 bucket chưa được cấu hình cho {category}. Kiểm tra section S3 trong appsettings.json.");
            }

            return bucket;
        }

        private static string BuildObjectKey(StorageCategory category, string fileName)
        {
            var prefix = category switch
            {
                StorageCategory.EventImage => "events",
                StorageCategory.GiftImage => "gifts",
                StorageCategory.Video => "videos",
                StorageCategory.Certificate => "certificates",
                StorageCategory.Assignment => "assignments",
                StorageCategory.Evidence => "evidence",
                StorageCategory.CertificateTemplate => "templates",
                _ => "misc"
            };

            return $"{prefix}/{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        }

        private static string FormatStoredPath(string bucket, string key) => $"s3:{bucket}/{key}";

        private static bool TryParseStoredPath(string storedPath, out string bucket, out string key)
        {
            bucket = string.Empty;
            key = string.Empty;

            if (!storedPath.StartsWith("s3:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = storedPath[3..];
            var slashIndex = payload.IndexOf('/');
            if (slashIndex <= 0)
            {
                return false;
            }

            bucket = payload[..slashIndex];
            key = payload[(slashIndex + 1)..];
            return !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(key);
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
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
