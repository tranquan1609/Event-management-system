using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Repositories.Implementations
{
    public class EFLocationRepository : ILocationRepository
    {
        private readonly ApplicationDbContext _context;

        public EFLocationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Location>> GetAllAsync()
        {
            return await _context.Locations.ToListAsync();
        }

        public async Task<Location?> GetByIdAsync(int id)
        {
            return await _context.Locations.FindAsync(id);
        }

        public async Task<Location?> GetByIdWithEventsAsync(int id)
        {
            // Include Events when fetching the Location
            return await _context.Locations
                .Include(l => l.Events) // Include related Events
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task AddAsync(Location location)
        {
            _context.Locations.Add(location);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Location location)
        {
            _context.Locations.Update(location);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var location = await GetByIdAsync(id);
            if (location != null)
            {
                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();
            }
        }
    }
}
