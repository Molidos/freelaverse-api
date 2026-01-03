using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FreelaverseApi.Models;

public class Service : IAuditable
{
    public Guid Id { get; set;} = Guid.NewGuid();
    public string Title {get; set;} = string.Empty;//Título do serviço
    public string Description {get; set;} = string.Empty;//Descrição do serviço
    public string Category {get; set;} = string.Empty;//Categoria do serviço
    public string Urgency {get; set;} = string.Empty;//Urgência do serviço
    public string Status {get; set;} = string.Empty;//Status do serviço
    public string Address {get; set;} = string.Empty;//Endereço do serviço
    public Guid UserId {get; set;} = Guid.Empty;//ID do usuário que solicitou o serviço
    [JsonIgnore]
    public User? Client {get; set; }//Cliente que solicitou o serviço
    public Guid ClientId {get; set;} = Guid.Empty;//ID do cliente que solicitou o serviço
    public List<ProfessionalService> ProfessionalService {get; set;} = new List<ProfessionalService>();//Profissionais que desbloquearam o serviço
    public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow;//Data de criação do serviço
    public DateTimeOffset UpdatedAt {get; set;} = DateTimeOffset.UtcNow;//Data de atualização do serviço
}