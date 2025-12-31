using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IUserProfessionalAreaService
{
    Task<IEnumerable<UserProfessionalAreas>> GetAllAsync();
    Task<UserProfessionalAreas?> GetByIdAsync(Guid id);
    Task<UserProfessionalAreas> CreateAsync(UserProfessionalAreas relation);
    Task<UserProfessionalAreas?> UpdateAsync(Guid id, UserProfessionalAreas relation);
    Task<bool> DeleteAsync(Guid id);
}


