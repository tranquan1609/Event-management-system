using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Repositories.Implementations
{
    public class EFVideoRepository : IVideoRepository
    {
        private readonly ApplicationDbContext _context;

        public EFVideoRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Video> GetAllVideos()
        {
            return _context.Videos
                .Include(v => v.Event)
                .OrderByDescending(v => v.UploadDate)
                .ToList();
        }

        public Video? GetVideoById(int id)
        {
            return _context.Videos
                .Include(v => v.Event)
                .FirstOrDefault(v => v.Id == id);
        }

        public void AddVideo(Video video)
        {
            _context.Videos.Add(video);
            _context.SaveChanges();
        }

        public void UpdateVideo(Video video)
        {
            _context.Videos.Update(video);
            _context.SaveChanges();
        }

        public void DeleteVideo(int id)
        {
            var video = _context.Videos.Find(id);
            if (video != null)
            {
                _context.Videos.Remove(video);
                _context.SaveChanges();
            }
        }

        public IEnumerable<Video> GetVideosByEventId(int eventId)
        {
            return _context.Videos
                .Include(v => v.Event)
                .Where(v => v.EventId == eventId)
                .OrderByDescending(v => v.UploadDate)
                .ToList();
        }
    }
}