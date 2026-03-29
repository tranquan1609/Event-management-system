using DACSWEBSK.Models;

namespace DACSWEBSK.Repositories.Interfaces
{
    public interface IGiftRepository
    {
        Task<IEnumerable<Gift>> GetAllAsync();
        Task<Gift?> GetByIdAsync(int id);
        Task AddAsync(Gift gift);
        Task UpdateAsync(Gift gift);
        Task DeleteAsync(int id);
    }
}
