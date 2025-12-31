using System.ComponentModel.DataAnnotations;
namespace FreelaverseApi.Models;

public class ProfessionalService
{
    public Guid Id { get; set;} = Guid.NewGuid();
    public Guid ProfessionalId {get; set;} = Guid.Empty;//ID do profissional que presta o serviço
    public User Professional {get; set;} = new User();//Profissional que presta o serviço
    public Guid ServiceId {get; set;} = Guid.Empty;//ID do serviço que o profissional presta
    public Service Service {get; set;} = new Service();//Serviço que o profissional presta
}