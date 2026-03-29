using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy sự kiện sắp diễn ra (Upcoming)
            ViewBag.Events = await _context.Events
                .Include(e => e.Location)
                .Where(e => e.Status == "Upcoming")
                .OrderBy(e => e.StartTime)
                .Take(6)
                .ToListAsync();

            // Lấy sự kiện đang diễn ra (Ongoing)
            ViewBag.OngoingEvents = await _context.Events
                .Include(e => e.Location)
                .Where(e => e.Status == "Ongoing")
                .OrderBy(e => e.StartTime)
                .Take(6)
                .ToListAsync();

            ViewBag.Videos = await _context.Videos
                .Include(v => v.Event)
                .OrderByDescending(v => v.Id)
                .Take(5)
                .ToListAsync();

            ViewBag.Notifications = await _context.Notifications
                .Include(n => n.Event)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }
    }
}