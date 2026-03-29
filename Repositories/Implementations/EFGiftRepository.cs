using DACSWEBSK.Models;
using DACSWEBSK.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Repositories.Implementations
{
    public class EFGiftRepository : IGiftRepository
    {
        private readonly ApplicationDbContext _context;

        public EFGiftRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Gift>> GetAllAsync()
        {
            return await _context.Gifts.ToListAsync();
        }

        public async Task<Gift?> GetByIdAsync(int id)
        {
            return await _context.Gifts.FindAsync(id);
        }

        public async Task AddAsync(Gift gift)
        {
            _context.Gifts.Add(gift);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Gift gift)
        {
            _context.Gifts.Update(gift);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var gift = await _context.Gifts.FindAsync(id);
            if (gift != null)
            {
                _context.Gifts.Remove(gift);
                await _context.SaveChangesAsync();
            }
        }
    }
}
