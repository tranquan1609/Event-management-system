using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DACSWEBSK.Services.Jobs
{
    /// <summary>
    /// Lightweight chatbot for Lambda: RDS only, no OpenAI (VPC has no internet).
    /// </summary>
    public static class ChatbotJob
    {
        public static async Task<string> RunAsync(ApplicationDbContext db, string userMessage, ILogger? logger = null)
        {
            var message = userMessage.Trim().ToLowerInvariant();
            logger?.LogInformation("ChatbotJob processing: {Message}", message);

            if (message.Contains("su kien") || message.Contains("sự kiện") || message.Contains("event"))
            {
                return await GetEventsReplyAsync(db, message);
            }

            if (message.Contains("qua") || message.Contains("quà") || message.Contains("gift"))
            {
                return await GetGiftsReplyAsync(db);
            }

            if (message.Contains("dia diem") || message.Contains("địa điểm") || message.Contains("location"))
            {
                return await GetLocationsReplyAsync(db);
            }

            return "Xin chào! Tôi là DACS Assistant. Bạn có thể hỏi về:\n" +
                   "- Sự kiện sắp tới / đang diễn ra\n" +
                   "- Quà tặng\n" +
                   "- Địa điểm";
        }

        private static async Task<string> GetEventsReplyAsync(ApplicationDbContext db, string message)
        {
            var query = db.Events.Include(e => e.Location).AsQueryable();
            string eventType;

            if (message.Contains("dang dien ra") || message.Contains("đang diễn ra"))
            {
                query = query.Where(e => e.StartTime <= DateTime.Now && e.EndTime >= DateTime.Now);
                eventType = "đang diễn ra";
            }
            else if (message.Contains("da dien ra") || message.Contains("đã diễn ra") || message.Contains("ket thuc"))
            {
                query = query.Where(e => e.EndTime < DateTime.Now);
                eventType = "đã diễn ra";
            }
            else if (message.Contains("tat ca") || message.Contains("tất cả") || message.Contains("hien co") || message.Contains("hiện có"))
            {
                eventType = "hiện có";
            }
            else
            {
                query = query.Where(e => e.StartTime > DateTime.Now);
                eventType = "sắp tới";
            }

            var events = await query.OrderBy(e => e.StartTime).Take(10).ToListAsync();
            if (!events.Any())
            {
                return $"Hiện tại không có sự kiện {eventType}.";
            }

            var lines = events.Select(e =>
                $"- {e.Title}: {e.StartTime:dd/MM/yyyy} – {e.EndTime:dd/MM/yyyy}" +
                (e.Location != null ? $" ({e.Location.Name})" : ""));

            return $"Các sự kiện {eventType}:\n" + string.Join("\n", lines);
        }

        private static async Task<string> GetGiftsReplyAsync(ApplicationDbContext db)
        {
            var gifts = await db.Gifts
                .Where(g => g.RequiredPoints != null && g.RequiredPoints > 0)
                .OrderBy(g => g.RequiredPoints)
                .Take(10)
                .ToListAsync();

            if (!gifts.Any())
            {
                return "Hiện tại chưa có quà tặng nào.";
            }

            var lines = gifts.Select(g => $"- {g.Name}: {g.RequiredPoints} điểm");
            return "Các quà tặng hiện có:\n" + string.Join("\n", lines);
        }

        private static async Task<string> GetLocationsReplyAsync(ApplicationDbContext db)
        {
            var locations = await db.Locations.Take(5).ToListAsync();
            if (!locations.Any())
            {
                return "Chưa có thông tin địa điểm.";
            }

            var lines = locations.Select(l => $"- {l.Name}: {l.Address}");
            return "Các địa điểm:\n" + string.Join("\n", lines);
        }
    }
}
