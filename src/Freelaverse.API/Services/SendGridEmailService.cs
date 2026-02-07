using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using Freelaverse.API.Options;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Freelaverse.API.Services;

public class SendGridEmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly SendGridOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SendGridEmailService> _logger;
    private string? _cachedTemplate;
    private string? _cachedLogoHtml;
    private bool _logoFileChecked = false;

    public SendGridEmailService(
        ISendGridClient client,
        IOptions<SendGridOptions> options,
        IWebHostEnvironment env,
        ILogger<SendGridEmailService> logger)
    {
        _client = client;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task SendEmailConfirmationCodeAsync(User user, string code)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("SendGrid API key is not configured.");

        var fromEmail = string.IsNullOrWhiteSpace(_options.FromEmail)
            ? "no-reply@freelaverse.com"
            : _options.FromEmail;

        var fromName = string.IsNullOrWhiteSpace(_options.FromName)
            ? "Freelaverse"
            : _options.FromName;

        var msg = new SendGridMessage
        {
            From = new EmailAddress(fromEmail, fromName),
            Subject = "Seu código de confirmação - Freelaverse",
            HtmlContent = BuildHtml(user, code)
        };

        msg.AddTo(new EmailAddress(user.Email, user.UserName));

        var response = await _client.SendEmailAsync(msg);

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var body = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid returned non-accepted status {Status}. Body: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"SendGrid failed with status {response.StatusCode}");
        }
        else
        {
            _logger.LogInformation("SendGrid accepted email to {Email}", user.Email);
        }
    }

    private string BuildHtml(User user, string code)
    {
        var template = LoadTemplate();
        var logoUrl = _options.LogoUrl ?? string.Empty;
        var logoBlock = ResolveLogoHtml(logoUrl);

        return template
            .Replace("{{Logo}}", logoBlock)
            .Replace("{{UserName}}", WebUtility.HtmlEncode(user.UserName))
            .Replace("{{Code}}", WebUtility.HtmlEncode(code))
            .Replace("{{ExpiresMinutes}}", "1");
    }

    private string LoadTemplate()
    {
        if (!string.IsNullOrWhiteSpace(_cachedTemplate))
            return _cachedTemplate!;

        var path = Path.Combine(_env.ContentRootPath, "EmailTemplates", "EmailConfirmation.html");
        if (File.Exists(path))
        {
            _cachedTemplate = File.ReadAllText(path);
            return _cachedTemplate!;
        }

        // Fallback simples se o template não existir.
        _cachedTemplate = """
        <div style="background:transparent;padding:32px 0;font-family:'Inter',Arial,sans-serif;color:#0b1021;">
          <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center" width="100%" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 8px 28px rgba(0,0,0,0.12);border:1px solid rgba(0,0,0,0.06);">
            <tr><td style="padding:32px 32px 20px;text-align:center;">{{Logo}}<h1 style="margin:0;font-size:22px;color:#0b1021;">Confirme seu email</h1><p style="margin:12px 0 0;color:#4a4f5c;font-size:14px;line-height:1.6;">Olá, {{UserName}}! Use o código abaixo para ativar sua conta.</p></td></tr>
            <tr><td style="padding:10px 32px 24px;text-align:center;"><div style="display:inline-block;background:#f5f5f8;border:1px solid rgba(0,0,0,0.06);border-radius:14px;padding:18px 24px;"><div style="font-size:28px;font-weight:700;letter-spacing:8px;color:#6c2bd9;font-family:'SFMono-Regular',Consolas,'Liberation Mono',Menlo,monospace;">{{Code}}</div><div style="margin-top:8px;color:#4a4f5c;font-size:12px;">Código expira em {{ExpiresMinutes}} minutos</div></div></td></tr>
            <tr><td style="padding:0 32px 28px;text-align:left;"><div style="background:rgba(108,43,217,0.06);border:1px solid rgba(108,43,217,0.2);border-radius:12px;padding:14px 16px;font-size:13px;color:#4a4f5c;line-height:1.6;">Se você não solicitou este cadastro, ignore este email.</div></td></tr>
            <tr><td style="padding:0 32px 28px;text-align:center;color:#4a4f5c;font-size:12px;">© Freelaverse — Segurança em primeiro lugar.</td></tr>
          </table>
        </div>
        """;
        return _cachedTemplate!;
    }

    private string ResolveLogoHtml(string logoUrl)
    {
        if (!string.IsNullOrWhiteSpace(_cachedLogoHtml))
            return _cachedLogoHtml!;

        // Prioriza URL pública configurada
        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            _cachedLogoHtml = $"""<img src="{logoUrl}" alt="Freelaverse" style="max-width:140px; margin-bottom:16px;" />""";
            return _cachedLogoHtml!;
        }

        // Fallback: tenta carregar um arquivo local e embedar como data URI
        if (_logoFileChecked)
            return string.Empty;

        _logoFileChecked = true;
        var localPath = Path.Combine(_env.ContentRootPath, "EmailTemplates", "imgs", "logo.png");
        if (!File.Exists(localPath))
            return string.Empty;

        var bytes = File.ReadAllBytes(localPath);
        var base64 = Convert.ToBase64String(bytes);
        var mime = "image/png";
        _cachedLogoHtml = $"""<img src="data:{mime};base64,{base64}" alt="Freelaverse" style="max-width:140px; margin-bottom:16px;" />""";
        return _cachedLogoHtml!;
    }
}
