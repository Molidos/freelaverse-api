using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Freelaverse.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using FreelaverseApi.Models;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;
using BillingPortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;
using CheckoutSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using CheckoutSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using CheckoutSessionLineItemPriceDataOptions = Stripe.Checkout.SessionLineItemPriceDataOptions;
using CheckoutSessionLineItemPriceDataProductDataOptions = Stripe.Checkout.SessionLineItemPriceDataProductDataOptions;
using CheckoutSessionLineItemPriceDataRecurringOptions = Stripe.Checkout.SessionLineItemPriceDataRecurringOptions;
using Stripe.Checkout;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StripeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserSubscriptionService _subscriptionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeController> _logger;

    public StripeController(
        IUserService userService,
        IUserSubscriptionService subscriptionService,
        IConfiguration configuration,
        ILogger<StripeController> logger)
    {
        _userService = userService;
        _subscriptionService = subscriptionService;
        _configuration = configuration;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("url")]
    public async Task<IActionResult> GetStripeUrl()
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(new { error = "Unauthorized" });

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user is null)
            return Unauthorized(new { error = "Unauthorized" });
        if (user.UserType != UserType.Professional)
            return Forbid("Assinatura disponível apenas para usuários profissionais.");

        var stripeSection = _configuration.GetSection("Stripe");
        var secretKey = _configuration["STRIPE_API_KEY"] ?? stripeSection["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("Stripe:SecretKey não configurado.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Stripe não configurado" });
        }

        StripeConfiguration.ApiKey = secretKey;

        var accountUrl = stripeSection["AccountUrl"] ?? stripeSection["ReturnUrl"] ?? "http://localhost:3000/professional/account";
        var successUrl = stripeSection["SuccessUrl"] ?? accountUrl;
        var cancelUrl = stripeSection["CancelUrl"] ?? accountUrl;

        try
        {
            var subscription = await _subscriptionService.GetByUserIdAsync(user.Id);
            var hasActiveSubscription = !string.IsNullOrWhiteSpace(subscription?.StripeSubscriptionId);

            if (hasActiveSubscription && subscription?.StripeCustomerId is { Length: > 0 } existingCustomerId)
            {
                // Se já existe cliente (e possivelmente assinatura), abre o Billing Portal
                var portalService = new BillingPortalSessionService();
                var portalSession = await portalService.CreateAsync(new BillingPortalSessionCreateOptions
                {
                    Customer = existingCustomerId,
                    ReturnUrl = accountUrl
                });

                return Ok(new { url = portalSession.Url });
            }

            var customerId = subscription?.StripeCustomerId;
            if (string.IsNullOrWhiteSpace(customerId))
            {
                var customerService = new CustomerService();
                var customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = user.Email,
                    Name = user.UserName,
                    Metadata = new Dictionary<string, string>
                    {
                        ["userId"] = user.Id.ToString()
                    }
                });

                await _subscriptionService.UpsertAsync(user.Id, customer.Id);
                customerId = customer.Id;
            }

            var checkoutService = new CheckoutSessionService();
            var stripeSession = await checkoutService.CreateAsync(new CheckoutSessionCreateOptions
            {
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "subscription",
                BillingAddressCollection = "auto",
                Customer = customerId,
                CustomerEmail = string.IsNullOrWhiteSpace(customerId) ? user.Email : null,
                LineItems = new List<CheckoutSessionLineItemOptions>
                {
                    new CheckoutSessionLineItemOptions
                    {
                        PriceData = new CheckoutSessionLineItemPriceDataOptions
                        {
                            Currency = "brl",
                            ProductData = new CheckoutSessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Plano Freelaverse",
                                Description = "Garanta o acesso a todas as oportunidades de emprego e freelancers a partir de um valor fixo mensal."
                            },
                            UnitAmount = 100,
                            Recurring = new CheckoutSessionLineItemPriceDataRecurringOptions
                            {
                                Interval = "month"
                            }
                        },
                        Quantity = 1
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = user.Id.ToString()
                }
            });

            return Ok(new { url = stripeSession.Url });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Erro do Stripe ao gerar sessão para o usuário {UserId}", user.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Erro ao processar pagamento", stripeError = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro interno ao gerar sessão do Stripe para o usuário {UserId}", user.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Erro interno" });
        }
    }

    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetSubscriptionStatus()
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(new { error = "Unauthorized" });

        var sub = await _subscriptionService.GetByUserIdAsync(userId.Value);
        var hasSubscription = sub is not null && !string.IsNullOrWhiteSpace(sub.StripeSubscriptionId);

        return Ok(new
        {
            hasSubscription,
            stripeCustomerId = sub?.StripeCustomerId,
            stripeSubscriptionId = sub?.StripeSubscriptionId,
            stripePriceId = sub?.StripePriceId,
            stripeCurrentPeriodEnd = sub?.StripeCurrentPeriodEnd
        });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        var stripeSection = _configuration.GetSection("Stripe");
        var webhookSecret = _configuration["STRIPE_WEBHOOK_SECRET"] ?? stripeSection["WebhookSecret"];
        var secretKey = _configuration["STRIPE_API_KEY"] ?? stripeSection["SecretKey"];

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogError("Stripe webhook secret não configurado.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Stripe webhook não configurado" });
        }

        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            StripeConfiguration.ApiKey = secretKey;
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar webhook do Stripe.");
            return BadRequest(new { error = $"Webhook Error: {ex.Message}" });
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case Events.CheckoutSessionCompleted:
                case Events.InvoicePaymentSucceeded:
                    await HandleSubscriptionUpdated(stripeEvent);
                    break;
                default:
                    _logger.LogInformation("Webhook Stripe ignorado. Tipo: {Type}", stripeEvent.Type);
                    break;
            }

            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar webhook do Stripe.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Erro interno ao processar webhook" });
        }
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        // checkout.session.completed traz Session; invoice.payment_succeeded traz Invoice
        string? subscriptionId = null;
        string? customerId = null;
        string? userIdMetadata = null;

        if (stripeEvent.Data.Object is Session session)
        {
            subscriptionId = session.Subscription?.ToString();
            customerId = session.Customer?.ToString();
            userIdMetadata = session.Metadata.TryGetValue("userId", out var uid) ? uid : null;
        }
        else if (stripeEvent.Data.Object is Invoice invoice)
        {
            subscriptionId = invoice.SubscriptionId;
            customerId = invoice.CustomerId;
            userIdMetadata = invoice.Metadata.TryGetValue("userId", out var uid) ? uid : null;
        }

        if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(customerId))
        {
            _logger.LogWarning("Webhook Stripe sem subscriptionId ou customerId. Tipo: {Type}", stripeEvent.Type);
            return;
        }

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(subscriptionId);

        // Recupera userId do metadata; se não vier, tenta buscar pelo customerId
        if (string.IsNullOrWhiteSpace(userIdMetadata))
        {
            userIdMetadata = subscription.Metadata.TryGetValue("userId", out var uid) ? uid : null;
        }

        Guid userId;
        if (!Guid.TryParse(userIdMetadata, out userId))
        {
            var subRecord = await _subscriptionService.GetByCustomerIdAsync(customerId);
            if (subRecord is null)
            {
                _logger.LogWarning("Webhook Stripe sem userId válido no metadata e sem correspondência de customerId. Subscription: {SubscriptionId}", subscriptionId);
                return;
            }
            userId = subRecord.UserId;
        }

        var priceId = subscription.Items.Data.FirstOrDefault()?.Price?.Id ?? string.Empty;
        var currentPeriodEnd = new DateTimeOffset(subscription.CurrentPeriodEnd);

        await _subscriptionService.UpsertFromWebhookAsync(
            userId,
            customerId,
            subscriptionId,
            priceId,
            currentPeriodEnd);
    }

    private Guid? GetUserIdFromToken()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(sub, out var id))
            return id;

        return null;
    }
}

// Corpo não é necessário; rota usa apenas o usuário autenticado.
