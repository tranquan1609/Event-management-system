using DACSWEBSK.Models;

namespace DACSWEBSK.Services
{
    public interface IVideoProgressService
    {
        Task<VideoProgress> GetProgressAsync(int videoId, int attendeeId);
        Task UpdateVideoProgressAsync(int videoId, int attendeeId, int currentTime);
        Task UpdateLivestreamProgressAsync(int videoId, int attendeeId, DateTime? joinTime, DateTime? leaveTime);
        Task<bool> CheckCertificateEligibilityAsync(int attendeeId, int eventId);
        Task<IEnumerable<VideoProgress>> GetAttendeeProgressesAsync(int attendeeId, int eventId);
    }
} 