using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace DACSWEBSK.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminLocationController : Controller
    {
        private readonly ILocationRepository _locationRepository;

        public AdminLocationController(ILocationRepository locationRepository)
        {
            _locationRepository = locationRepository;
        }

        public async Task<IActionResult> Index()
        {
            var locations = await _locationRepository.GetAllAsync();
            return View(locations);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Location model)
        {
            if (ModelState.IsValid)
            {
                await _locationRepository.AddAsync(model);
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null) return NotFound();
            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Location model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                await _locationRepository.UpdateAsync(model);
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var location = await _locationRepository.GetByIdWithEventsAsync(id);
            if (location == null) return NotFound();
            return View(location);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null) return NotFound();
            return View(location);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _locationRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
