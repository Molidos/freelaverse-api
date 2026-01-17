namespace Freelaverse.API.Options;

public class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Freelaverse";
    public string? LogoUrl { get; set; }
}
