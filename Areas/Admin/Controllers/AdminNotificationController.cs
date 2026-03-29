using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminNotificationController : Controller
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IEventRepository _eventRepository;

        public AdminNotificationController(INotificationRepository notificationRepository, IEventRepository eventRepository)
        {
            _notificationRepository = notificationRepository;
            _eventRepository = eventRepository;
        }

        public async Task<IActionResult> Index()
        {
            var notifications = await _notificationRepository.GetAllAsync();
            return View(notifications);
        }

        public async Task<IActionResult> Create()
        {
            var events = await _eventRepository.GetAllAsync();
            ViewData["EventId"] = new SelectList(events, "Id", "Title");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Notification model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                await _notificationRepository.AddAsync(model);
                return RedirectToAction(nameof(Index));
            }

            var events = await _eventRepository.GetAllAsync();
            ViewData["EventId"] = new SelectList(events, "Id", "Title", model.EventId);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null) return NotFound();

            var events = await _eventRepository.GetAllAsync();
            ViewData["EventId"] = new SelectList(events, "Id", "Title", notification.EventId);
            return View(notification);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Notification model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                await _notificationRepository.UpdateAsync(model);
                return RedirectToAction(nameof(Index));
            }

            var events = await _eventRepository.GetAllAsync();
            ViewData["EventId"] = new SelectList(events, "Id", "Title", model.EventId);
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null) return NotFound();
            return View(notification);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null) return NotFound();
            return View(notification);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _notificationRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
