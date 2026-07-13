namespace DACSWEBSK.Services.Storage
{
    public class StorageOptions
    {
        public const string SectionName = "Storage";

        /// <summary>Local = wwwroot (mặc định). S3 = Amazon S3.</summary>
        public string Provider { get; set; } = "Local";
    }

    public class S3StorageOptions
    {
        public const string SectionName = "S3";

        public string ImagesBucket { get; set; } = string.Empty;
        public string VideosBucket { get; set; } = string.Empty;
        public string CertificatesBucket { get; set; } = string.Empty;
        public string UploadsBucket { get; set; } = string.Empty;

        /// <summary>Thời gian pre-signed URL (phút) khi bucket không public.</summary>
        public int PresignedUrlExpiryMinutes { get; set; } = 60;
    }
}
