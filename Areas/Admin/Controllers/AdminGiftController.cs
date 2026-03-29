using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminGiftController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminGiftController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Danh sách phần quà
        public async Task<IActionResult> Index()
        {
            var gifts = await _context.Gifts.ToListAsync();
            return View(gifts);
        }

        // GET: Tạo mới phần quà
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tạo mới phần quà
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Gift gift, IFormFile ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "gifts");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }
                    gift.ImageUrl = "/images/gifts/" + uniqueFileName;
                }

                _context.Gifts.Add(gift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(gift);
        }

        // GET: Sửa phần quà
        public async Task<IActionResult> Edit(int id)
        {
            var gift = await _context.Gifts.FindAsync(id);
            if (gift == null) return NotFound();
            return View(gift);
        }

        // POST: Sửa phần quà
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Gift gift)
        {
            if (id != gift.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(gift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(gift);
        }

        // Xóa phần quà
        public async Task<IActionResult> Delete(int id)
        {
            var gift = await _context.Gifts.FindAsync(id);
            if (gift == null) return NotFound();
            _context.Gifts.Remove(gift);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
