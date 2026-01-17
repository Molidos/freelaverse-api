namespace Freelaverse.API.Models.Auth;

public class ConfirmEmailCodeRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
