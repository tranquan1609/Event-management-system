using DACSWEBSK.Models;

namespace DACSWEBSK.Repositories.Interfaces
{
    public interface IVideoRepository
    {
        IEnumerable<Video> GetAllVideos();
        Video? GetVideoById(int id);
        void AddVideo(Video video);
        void UpdateVideo(Video video);
        void DeleteVideo(int id);
        IEnumerable<Video> GetVideosByEventId(int eventId);
    }
} 