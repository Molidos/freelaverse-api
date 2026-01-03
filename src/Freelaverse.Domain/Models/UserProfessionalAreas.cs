using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FreelaverseApi.Models;

public class UserProfessionalAreas
{
    public Guid Id { get; set;} = Guid.NewGuid();
    public Guid UserId {get; set;} = Guid.Empty;//ID do usuário que possui a área de serviço
    [JsonIgnore]
    public User? User {get; set; }//Usuário que possui a área de serviço
    public Guid ProfessionalAreaId {get; set;} = Guid.Empty;//ID da área de serviço
    public ProfessionalAreas ProfessionalArea {get; set;} = new ProfessionalAreas();//Área de serviço
}