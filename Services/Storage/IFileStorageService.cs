namespace DACSWEBSK.Services.Storage
{
    public interface IFileStorageService
    {
        bool IsS3 { get; }

        Task<string> UploadAsync(IFormFile file, StorageCategory category);

        Task<string> UploadBytesAsync(byte[] data, StorageCategory category, string fileName, string contentType);

        Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(string storedPath);

        Task DeleteAsync(string storedPath);

        /// <summary>URL dùng trong thẻ img/video/a href. Hỗ trợ path local cũ và s3:...</summary>
        string GetAccessUrl(string? storedPath);

        string? GetLocalPathOrNull(string storedPath);
    }
}
