using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PixController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PixController> _logger;

    public PixController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PixController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
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
                    name = request.Product,
                    quantity = request.Quantity,
                    unit_amount = request.UnitAmount
                }
            },
            qr_codes = new[]
            {
                new
                {
                    amount = new { value = request.Price },
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

            return Ok(new { qrText, qrLink });
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
}

public class PixRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int UnitAmount { get; set; } = 500; // valor em centavos
    public int Price { get; set; } = 500; // valor em centavos para o QR
    public DateTime? ExpirationDate { get; set; }
    public string? TaxId { get; set; }
    public object? Address { get; set; }
}
