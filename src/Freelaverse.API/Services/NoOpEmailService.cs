using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;

namespace Freelaverse.API.Services;

/// <summary>
/// Serviço de email que não envia nada. Útil em ambientes locais/CI sem SendGrid.
/// </summary>
public class NoOpEmailService : IEmailService
{
    public Task SendEmailConfirmationCodeAsync(User user, string code)
    {
        return Task.CompletedTask;
    }
}
