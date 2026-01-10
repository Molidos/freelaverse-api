using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IServiceService
{
    Task<IEnumerable<Service>> GetAllAsync();
    Task<Service?> GetByIdAsync(Guid id);
    Task<Service?> GetByIdWithClientAsync(Guid id);
    Task<Service> CreateAsync(Service service);
    Task<Service?> UpdateAsync(Guid id, Service service);
    Task<bool> DeleteAsync(Guid id);
    Task<(IEnumerable<Service> Items, int TotalCount)> GetByCategoryPagedAsync(IEnumerable<string>? categories, int page, int pageSize);
}


