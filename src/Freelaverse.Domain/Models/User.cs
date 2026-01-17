using System.ComponentModel.DataAnnotations;

namespace FreelaverseApi.Models;

public class User : IAuditable
{
    public Guid Id { get; set;} = Guid.NewGuid();
    public string UserName {get; set;} = string.Empty;
    [Required, MaxLength(30)]
    public string Email {get; set;} = string.Empty;
    public string Password {get; set;} = string.Empty;
    public bool EmailConfirmed { get; set; } = false;
    public string? EmailConfirmationToken { get; set; }
    public DateTimeOffset? EmailConfirmationTokenExpiresAt { get; set; }

    public UserType UserType { get; set;}
    [Required, MaxLength(120)]
    public string? ProfileImageUrl {get; set;}
    //Address
    public string Street {get; set;} = string.Empty;
    public string Number {get; set;} = string.Empty;
    public string Complement {get; set;} = string.Empty;
    public string ZipCode {get; set;} = string.Empty;
    public string City {get; set;} = string.Empty;
    public string State {get; set;} = string.Empty;
    //Contacts
    public string Phone {get; set;} = string.Empty;
    public int Credits { get; set; } = 0;
    //Services
    public List<Service> ClientServices {get; set;} = new List<Service>();//Serviços solicitados pelo cliente
    public List<ProfessionalService> ProfessionalService {get; set;} = new List<ProfessionalService>();//Serviços prestados pelo profissional
    public List<UserProfessionalAreas> UserProfessionalArea {get; set;} = new List<UserProfessionalAreas>();//Áreas de serviço do profissional
    public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt {get; set;} = DateTimeOffset.UtcNow;
}