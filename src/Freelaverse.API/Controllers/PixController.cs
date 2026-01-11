using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Freelaverse.Services.Interfaces;
using Freelaverse.API.Hubs;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PixController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PixController> _logger;
    private readonly IUserService _userService;
    private readonly IHubContext<PaymentsHub> _hubContext;

    public PixController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PixController> logger,
        IUserService userService,
        IHubContext<PaymentsHub> hubContext)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _userService = userService;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePix([FromBody] PixRequest request, CancellationToken cancellationToken)
    {
        var auth = _configuration.GetValue<string>("PagSeguro:Authorization");
        if (string.IsNullOrWhiteSpace(auth))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "PagSeguro Authorization não configurado (PagSeguro:Authorization)." });
        }

        var notificationUrl = _configuration.GetValue<string>("PagSeguro:NotificationUrl");

        // Catálogo fixo de pacotes (server-side para evitar manipulação de preço)
        var packs = new Dictionary<string, (string Label, int Credits, int PriceCents)>(StringComparer.OrdinalIgnoreCase)
        {
            ["pack1"] = ("1.000 créditos", 1000, /*4990*/100),
            ["pack2"] = ("2.000 créditos", 2000, /*8990*/1000),
            ["pack3"] = ("3.000 créditos", 3000, 11990)
        };

        if (string.IsNullOrWhiteSpace(request.PackId) || !packs.TryGetValue(request.PackId, out var pack))
        {
            return BadRequest(new { error = "Pacote inválido." });
        }

        var payload = new
        {
            reference_id = $"pix-{Guid.NewGuid()}",
            customer = new
            {
                name = request.Name,
                email = request.Email,
                tax_id = request.TaxId ?? "12345678909",
                phones = new[]
                {
                    new
                    {
                        country = "55",
                        area = "11",
                        number = "999999999",
                        type = "MOBILE"
                    }
                }
            },
            items = new[]
            {
                new
                {
                    name = pack.Label,
                    quantity = 1,
                    unit_amount = pack.PriceCents
                }
            },
            qr_codes = new[]
            {
                new
                {
                    amount = new { value = pack.PriceCents },
                    expiration_date = request.ExpirationDate
                }
            },
            shipping = new
            {
                address = request.Address ?? new
                {
                    street = "Avenida Brigadeiro Faria Lima",
                    number = "1384",
                    complement = "apto 12",
                    locality = "Pinheiros",
                    city = "São Paulo",
                    region_code = "SP",
                    country = "BRA",
                    postal_code = "01452002"
                }
            },
            notification_urls = string.IsNullOrWhiteSpace(notificationUrl)
                ? Array.Empty<string>()
                : new[] { notificationUrl }
        };

        var client = _httpClientFactory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.pagseguro.com/orders");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.SendAsync(message, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro PagSeguro: {Status} {Content}", response.StatusCode, content);
                return StatusCode((int)response.StatusCode, new { error = "Erro ao criar cobrança PIX", response = content });
            }

            using var doc = JsonDocument.Parse(content);
            var qrCodes = doc.RootElement.GetProperty("qr_codes");
            var qrText = qrCodes[0].GetProperty("text").GetString();
            var qrLink = qrCodes[0].GetProperty("links")[0].GetProperty("href").GetString();

            return Ok(new { qrText, qrLink, packId = request.PackId, pack.Label, pack.Credits, pack.PriceCents });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar PagSeguro PIX");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Erro interno ao gerar PIX" });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public IActionResult Webhook([FromBody] JsonElement body)
    {
        // Aqui você processa o evento de compensação do PagSeguro.
        // Apenas log para referência inicial.
        _logger.LogInformation("Webhook PagSeguro recebido: {Body}", body.ToString());
        return Ok(new { received = true });
    }

    /// <summary>
    /// Webhook de confirmação de pagamento PIX (PagSeguro) para creditar o usuário.
    /// Idempotência mínima: apenas processa quando status = PAID/CAPTURED e email está presente.
    /// Futuramente podemos adicionar websocket/SignalR para notificar o frontend.
    /// </summary>
    [HttpPost("confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPix([FromBody] JsonElement body)
    {
        _logger.LogInformation("ConfirmPix payload: {Body}", body.ToString());

        try
        {
            // Verifica status do pagamento
            var status = body.TryGetProperty("charges", out var chargesElement)
                && chargesElement.ValueKind == JsonValueKind.Array
                && chargesElement.GetArrayLength() > 0
                && chargesElement[0].TryGetProperty("status", out var statusProp)
                ? statusProp.GetString()
                : null;

            if (!string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "CAPTURED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Pagamento ignorado - status não pago: {Status}", status);
                return Ok(new { ignored = true, reason = "Status não pago" });
            }

            // Extrai email do cliente
            var email = body.TryGetProperty("customer", out var customerElement)
                && customerElement.TryGetProperty("email", out var emailProp)
                ? emailProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Webhook sem email do cliente. Payload: {Body}", body.ToString());
                return Ok(new { ignored = true, reason = "Email ausente" });
            }

            var user = await _userService.GetByEmailAsync(email);
            if (user is null)
            {
                _logger.LogWarning("Usuário não encontrado para email {Email}", email);
                return Ok(new { ignored = true, reason = "Usuário não encontrado" });
            }

            // Determina créditos a partir do nome do item (ex.: "1.000 créditos")
            int creditsToAdd = 0;
            if (body.TryGetProperty("items", out var itemsElement) &&
                itemsElement.ValueKind == JsonValueKind.Array &&
                itemsElement.GetArrayLength() > 0)
            {
                var name = itemsElement[0].TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString() ?? string.Empty
                    : string.Empty;

                // Remove tudo que não for número para capturar "1.000" -> "1000"
                var digits = Regex.Replace(name, "[^0-9]", "");
                if (!string.IsNullOrWhiteSpace(digits) && int.TryParse(digits, out var parsed))
                {
                    creditsToAdd = parsed;
                }
            }

            if (creditsToAdd <= 0)
            {
                _logger.LogWarning("Créditos não identificados no payload para email {Email}. Nenhuma alteração feita.", email);
                return Ok(new { ignored = true, reason = "Créditos não identificados" });
            }

            var updated = await _userService.AddCreditsAsync(user.Id, creditsToAdd);
            if (updated is null)
            {
                _logger.LogError("Falha ao creditar usuário {UserId}", user.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Falha ao creditar usuário" });
            }

            // Notifica frontend via SignalR para o grupo do usuário
            var group = PaymentsHub.GetGroup(email);
            await _hubContext.Clients.Group(group).SendAsync("PixPaymentUpdated", new
            {
                email,
                status = "paid",
                creditsAdded = creditsToAdd,
                totalCredits = updated.Credits
            });

            return Ok(new
            {
                message = "Pagamento PIX confirmado",
                email,
                creditsAdded = creditsToAdd,
                totalCredits = updated.Credits
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar webhook de confirmação PIX");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Erro ao processar webhook PIX" });
        }
    }
}

public class PixRequest
{
    public string PackId { get; set; } = string.Empty; // pack1, pack2, pack3...
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
    public string? TaxId { get; set; }
    public object? Address { get; set; }
}
