namespace Freelaverse.API.Models.Auth;

public class RegisterRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int UserType { get; set; } = 1; // 1 = Client, 2 = Professional
    public string ProfileImageUrl { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Complement { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<Guid> UserProfessionalArea { get; set; } = new();
}


