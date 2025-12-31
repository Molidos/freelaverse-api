using System.ComponentModel.DataAnnotations;

namespace FreelaverseApi.Models;

public class ProfessionalAreas : IAuditable
{
    public Guid Id { get; set;} = Guid.NewGuid();
    public string Name {get; set;} = string.Empty;
    public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt {get; set;} = DateTimeOffset.UtcNow;
}