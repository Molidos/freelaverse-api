using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IProfessionalServiceService
{
    Task<IEnumerable<ProfessionalService>> GetAllAsync();
    Task<ProfessionalService?> GetByIdAsync(Guid id);
    Task<ProfessionalService> CreateAsync(ProfessionalService professionalService);
    Task<ProfessionalService?> UpdateAsync(Guid id, ProfessionalService professionalService);
    Task<bool> DeleteAsync(Guid id);
}


