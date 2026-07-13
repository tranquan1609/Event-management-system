using DACSWEBSK.Models;
using DACSWEBSK.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminGiftController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;

        public AdminGiftController(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task<IActionResult> Index()
        {
            var gifts = await _context.Gifts.ToListAsync();
            return View(gifts);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Gift gift, IFormFile ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    gift.ImageUrl = await _fileStorage.UploadAsync(ImageFile, StorageCategory.GiftImage);
                }

                _context.Gifts.Add(gift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(gift);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var gift = await _context.Gifts.FindAsync(id);
            if (gift == null) return NotFound();
            return View(gift);
        }

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
