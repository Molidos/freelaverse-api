using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Freelaverse.API.Models.Auth;
using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using FreelaverseApi.Data;
using Stripe;
using Microsoft.AspNetCore.Http;
using System;

namespace Freelaverse.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly IProfessionalAreaService _professionalAreaService;
        private readonly IUserProfessionalAreaService _userProfessionalAreaService;
        private readonly IUserSubscriptionService _userSubscriptionService;
        private readonly AppDbContext _db;
        private readonly IEmailService _emailService;

        public AuthController(
            IUserService userService,
            IConfiguration configuration,
            IProfessionalAreaService professionalAreaService,
            IUserProfessionalAreaService userProfessionalAreaService,
            IUserSubscriptionService userSubscriptionService,
            AppDbContext db,
            IEmailService emailService)
        {
            _userService = userService;
            _configuration = configuration;
            _professionalAreaService = professionalAreaService;
            _userProfessionalAreaService = userProfessionalAreaService;
            _userSubscriptionService = userSubscriptionService;
            _db = db;
            _emailService = emailService;
        }

        [HttpGet("me")]
        public async Task<ActionResult> Me()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(sub))
            {
                return Unauthorized(new { message = "Token de autenticação não fornecido ou inválido." });
            }

            if (!Guid.TryParse(sub, out var userId))
            {
                return Unauthorized(new { message = "O token fornecido possui um formato inválido." });
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user is null) return NotFound(new { message = "Usuário não encontrado." });

            // Projeção sem campos sensíveis/desnecessários
            var subscriptionInfo = await _userSubscriptionService.GetByUserIdAsync(user.Id);
            var hasSubscription = subscriptionInfo is not null && !string.IsNullOrWhiteSpace(subscriptionInfo.StripeSubscriptionId);

            if (!hasSubscription && !string.IsNullOrWhiteSpace(subscriptionInfo?.StripeCustomerId))
            {
                subscriptionInfo = await RefreshSubscriptionFromStripe(user, subscriptionInfo);
                hasSubscription = subscriptionInfo is not null && !string.IsNullOrWhiteSpace(subscriptionInfo.StripeSubscriptionId);
            }

            var result = new
            {
                userName = user.UserName,
                email = user.Email,
                userType = user.UserType,
                emailConfirmed = user.EmailConfirmed,
                credits = user.Credits,
                subscription = new
                {
                    hasSubscription,
                    stripeCustomerId = subscriptionInfo?.StripeCustomerId,
                    stripeSubscriptionId = subscriptionInfo?.StripeSubscriptionId,
                    stripePriceId = subscriptionInfo?.StripePriceId,
                    stripeCurrentPeriodEnd = subscriptionInfo?.StripeCurrentPeriodEnd
                },
                profileImageUrl = user.ProfileImageUrl,
                phone = user.Phone,
                clientServices = user.ClientServices?
                    .Select(s => new
                    {
                        id = s.Id,
                        title = s.Title,
                        description = s.Description,
                        category = s.Category,
                        urgency = s.Urgency,
                        status = s.Status,
                        address = s.Address,
                        quantProfessionals = s.QuantProfessionals,
                        createdAt = s.CreatedAt,
                        updatedAt = s.UpdatedAt,
                        professionalService = s.ProfessionalService?
                            .Select(ps => new
                            {
                                professionalId = ps.ProfessionalId,
                                serviceId = ps.ServiceId
                            })
                            .Cast<object>()
                            .ToList() ?? new List<object>()
                    })
                    .Cast<object>()
                    .ToList() ?? new List<object>(),
                professionalService = user.ProfessionalService?
                    .Select(ps => new
                    {
                        professionalId = ps.ProfessionalId,
                        service = new
                        {
                            id = ps.Service.Id,
                            title = ps.Service.Title,
                            description = ps.Service.Description,
                            category = ps.Service.Category,
                            urgency = ps.Service.Urgency,
                            status = ps.Service.Status,
                            address = ps.Service.Address,
                            QuantProfessionals = ps.Service.QuantProfessionals,
                            createdAt = ps.Service.CreatedAt,
                            updatedAt = ps.Service.UpdatedAt
                        }
                    })
                    .Cast<object>()
                    .ToList() ?? new List<object>(),
                userProfessionalArea = user.UserProfessionalArea?
                    .Select(upa => new
                    {
                        professionalArea = new
                        {
                            name = upa.ProfessionalArea.Name
                        }
                    })
                    .Cast<object>()
                    .ToList() ?? new List<object>(),

            };

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<ActionResult<object>> Register([FromBody] RegisterRequest request)
        {
            // verificar se já existe
            var existing = await _userService.GetByEmailAsync(request.Email);
            if (existing != null)
                return Conflict("Email já cadastrado.");

            if (request.UserType != (int)UserType.Client && request.UserType != (int)UserType.Professional)
                return BadRequest("userType inválido. Use 1 para Client, 2 para Professional.");

            // validar áreas informadas
            var areaIds = request.UserProfessionalArea?.Distinct().ToList() ?? new List<Guid>();
            if (request.UserType == (int)UserType.Professional && areaIds.Count == 0)
                return BadRequest("Profissional deve informar ao menos uma área de atuação (userProfessionalArea).");

            foreach (var areaId in areaIds)
            {
                var area = await _professionalAreaService.GetByIdAsync(areaId);
                if (area is null)
                    return BadRequest($"Área informada não existe: {areaId}");
            }

            var newUser = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                Password = request.Password,
                ProfileImageUrl = request.ProfileImageUrl,
                UserType = (UserType)request.UserType,
                Street = request.Street,
                Number = request.Number,
                Complement = request.Complement,
                ZipCode = request.ZipCode,
                City = request.City,
                State = request.State,
                Phone = request.Phone
            };

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var created = await _userService.CreateAsync(newUser);

                // criar relações UserProfessionalArea
                foreach (var areaId in areaIds)
                {
                    await _userProfessionalAreaService.CreateAsync(new UserProfessionalAreas
                    {
                        UserId = created.Id,
                        ProfessionalAreaId = areaId
                    });
                }

                await transaction.CommitAsync();

                await _emailService.SendEmailConfirmationCodeAsync(created, created.EmailConfirmationToken!);

                return Ok(new
                {
                    message = "Cadastro realizado. Enviamos um código de confirmação para seu email.",
                    emailConfirmationSent = true,
                    expiresInSeconds = 60
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

        }

        [HttpPost("resend-email-confirmation")]
        [AllowAnonymous]
        public async Task<ActionResult> ResendEmailConfirmation([FromBody] ResendEmailConfirmationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email é obrigatório." });

            var user = await _userService.GetByEmailAsync(request.Email);
            if (user is null)
                return NotFound(new { message = "Usuário não encontrado." });

            if (user.EmailConfirmed)
                return Ok(new { message = "Email já confirmado. Faça login." });

            var refreshed = await _userService.RefreshEmailConfirmationTokenAsync(user, TimeSpan.FromMinutes(1));
            if (refreshed?.EmailConfirmationToken is null)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Não foi possível gerar um novo código. Tente novamente." });

            await _emailService.SendEmailConfirmationCodeAsync(refreshed, refreshed.EmailConfirmationToken);

            return Ok(new
            {
                message = "Enviamos um novo código para seu email. Ele expira em 1 minuto.",
                expiresInSeconds = 60
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
        {
            var user = await _userService.GetByEmailAndPasswordAsync(request.Email, request.Password);
            if (user is null)
            {
                return Unauthorized();
            }

            if (!user.EmailConfirmed)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Confirme seu email para acessar o sistema." });
            }

            var subscription = await _userSubscriptionService.GetByUserIdAsync(user.Id);
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.UserName,
                    user.UserType,
                    user.ProfileImageUrl
                },
                subscription = new
                {
                    hasSubscription = subscription is not null && !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId),
                    stripeCustomerId = subscription?.StripeCustomerId,
                    stripeSubscriptionId = subscription?.StripeSubscriptionId,
                    stripePriceId = subscription?.StripePriceId,
                    stripeCurrentPeriodEnd = subscription?.StripeCurrentPeriodEnd
                }
            });
        }

        private string GenerateJwtToken(FreelaverseApi.Models.User user)
        {
            var jwtSection = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.UserName)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail([FromBody] ConfirmEmailCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { message = "Email e código são obrigatórios." });

            var user = await _userService.GetByEmailAsync(request.Email);
            if (user is null)
                return NotFound(new { message = "Usuário não encontrado." });

            if (user.EmailConfirmed)
                return Ok(new { message = "Email já confirmado. Faça login." });

            if (!string.Equals(user.EmailConfirmationToken, request.Code, StringComparison.Ordinal))
                return BadRequest(new { message = "Código inválido." });

            if (user.EmailConfirmationTokenExpiresAt.HasValue &&
                user.EmailConfirmationTokenExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                return BadRequest(new { message = "Código expirado. Solicite um novo cadastro." });
            }

            await _userService.MarkEmailConfirmedAsync(user);
            return Ok(new { message = "Email confirmado com sucesso. Você já pode fazer login." });
        }

        private async Task<UserSubscription?> RefreshSubscriptionFromStripe(FreelaverseApi.Models.User user, UserSubscription? subscriptionInfo)
        {
            var stripeSection = _configuration.GetSection("Stripe");
            var secretKey = _configuration["STRIPE_API_KEY"] ?? stripeSection["SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey)) return subscriptionInfo;
            if (subscriptionInfo is null || string.IsNullOrWhiteSpace(subscriptionInfo.StripeCustomerId)) return subscriptionInfo;

            StripeConfiguration.ApiKey = secretKey;
            var subService = new SubscriptionService();

            var list = await subService.ListAsync(new SubscriptionListOptions
            {
                Customer = subscriptionInfo.StripeCustomerId,
                Status = "active",
                Limit = 1
            });

            var active = list.Data.FirstOrDefault();
            if (active is null) return subscriptionInfo;

            var priceId = active.Items.Data.FirstOrDefault()?.Price?.Id ?? string.Empty;
            var currentPeriodEnd = new DateTimeOffset(active.CurrentPeriodEnd);

            return await _userSubscriptionService.UpsertFromWebhookAsync(
                user.Id,
                subscriptionInfo.StripeCustomerId,
                active.Id,
                priceId,
                currentPeriodEnd);
        }
    }
}
