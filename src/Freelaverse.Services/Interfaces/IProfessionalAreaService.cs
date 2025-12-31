using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IProfessionalAreaService
{
    Task<IEnumerable<ProfessionalAreas>> GetAllAsync();
    Task<ProfessionalAreas?> GetByIdAsync(Guid id);
    Task<ProfessionalAreas> CreateAsync(ProfessionalAreas area);
    Task<ProfessionalAreas?> UpdateAsync(Guid id, ProfessionalAreas area);
    Task<bool> DeleteAsync(Guid id);
}


