using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Services.Storage;

namespace DACSWEBSK.Services.Storage
{
    internal static class FileStorageResults
    {
        public static async Task<IActionResult?> TryFileResultAsync(
            IFileStorageService fileStorage,
            string? storedPath,
            string? downloadFileName = null)
        {
            if (string.IsNullOrEmpty(storedPath))
            {
                return null;
            }

            var opened = await fileStorage.OpenReadAsync(storedPath);
            if (opened == null)
            {
                return null;
            }

            var (stream, contentType, fileName) = opened.Value;
            return new FileStreamResult(stream, contentType)
            {
                FileDownloadName = downloadFileName ?? fileName
            };
        }
    }
}
