using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationCodeAsync(User user, string code);
}
