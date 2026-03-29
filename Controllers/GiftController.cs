using DACSWEBSK.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Controllers
{
    public class GiftController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GiftController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var gifts = await _context.Gifts.ToListAsync();

            int userPoints = 0;
            if (User.Identity.IsAuthenticated)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == User.Identity.Name);
                if (user != null)
                {
                    userPoints = user.Points;
                }
            }
            ViewBag.UserPoints = userPoints;

            return View(gifts);
        }


        // GET: Đổi quà
        public async Task<IActionResult> Redeem(int id)
        {
            var gift = await _context.Gifts.FindAsync(id);
            if (gift == null) return NotFound();
            return View(gift);
        }

        // POST: Đổi quà
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemConfirmed(int id, string userEmail)
        {
            var gift = await _context.Gifts.FindAsync(id);
            if (gift == null) return NotFound();

            // Kiểm tra điểm của user (giả sử có bảng UserPoints)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
            {
                TempData["RedeemMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("Index");
            }

            if (user.Points < (gift.RequiredPoints ?? 0))
            {
                TempData["RedeemMessage"] = "Bạn không đủ điểm để đổi quà này.";
                return RedirectToAction("Index");
            }

            // Trừ điểm và lưu lịch sử
            user.Points -= gift.RequiredPoints ?? 0;
            _context.GiftRedemptions.Add(new GiftRedemption
            {
                UserEmail = userEmail,
                GiftId = gift.Id,
                RedeemedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["RedeemMessage"] = "Đổi quà thành công!";
            return RedirectToAction("Index");
        }
    }

}
