using DACSWEBSK.Models;

namespace DACSWEBSK.Repositories.Interfaces
{
    public interface ILocationRepository
    {
        Task<IEnumerable<Location>> GetAllAsync();
        Task<Location?> GetByIdAsync(int id);
        Task<Location?> GetByIdWithEventsAsync(int id); // Includes related Events
        Task AddAsync(Location location);
        Task UpdateAsync(Location location);
        Task DeleteAsync(int id);
    }

}
