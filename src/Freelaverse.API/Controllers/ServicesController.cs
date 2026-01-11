using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using FreelaverseApi.Data;
using Microsoft.EntityFrameworkCore;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;
    private readonly IUserSubscriptionService _subscriptionService;
    private readonly IUserService _userService;
    private readonly IProfessionalServiceService _professionalServiceService;
    private readonly AppDbContext _context;

    public ServicesController(
        IServiceService serviceService,
        IUserSubscriptionService subscriptionService,
        IUserService userService,
        IProfessionalServiceService professionalServiceService,
        AppDbContext context)
    {
        _serviceService = serviceService;
        _subscriptionService = subscriptionService;
        _userService = userService;
        _professionalServiceService = professionalServiceService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Service>>> GetAll()
    {
        var services = await _serviceService.GetAllAsync();
        // Evita ciclos de serialização projetando somente os campos necessários
        var result = services.Select(s => ProjectService(s, includePhone: false));
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user is null || user.UserType != UserType.Professional)
            return Forbid();

        var service = await _serviceService.GetByIdWithClientAsync(id);
        if (service is null) return NotFound();

        var subscription = await _subscriptionService.GetByUserIdAsync(userId.Value);
        var hasSubscription = subscription is not null && !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId);

        var alreadyUnlocked = await _context.ProfessionalServices
            .AsNoTracking()
            .AnyAsync(ps => ps.ServiceId == id && ps.ProfessionalId == userId.Value);

        var includePhone = hasSubscription || alreadyUnlocked;

        return Ok(ProjectService(service, includePhone));
    }

    [Authorize]
    [HttpPost("{id:guid}/unlock")]
    public async Task<ActionResult<object>> UnlockService(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user is null || user.UserType != UserType.Professional)
            return Forbid();

        var service = await _serviceService.GetByIdWithClientAsync(id);
        if (service is null) return NotFound();

        // Já desbloqueado?
        var already = await _context.ProfessionalServices
            .AsNoTracking()
            .AnyAsync(ps => ps.ServiceId == id && ps.ProfessionalId == userId.Value);
        if (already)
            return Ok(new { message = "Pedido já desbloqueado.", service = ProjectService(service, includePhone: true) });

        var subscription = await _subscriptionService.GetByUserIdAsync(userId.Value);
        var hasSubscription = subscription is not null && !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId);

        var cost = service.Value;
        var hasCredits = user.Credits >= cost;

        if (!hasSubscription && !hasCredits)
            return BadRequest(new { error = "Você não é assinante e não possui créditos suficientes." });

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // cria vínculo
            await _professionalServiceService.CreateAsync(new ProfessionalService
            {
                ProfessionalId = userId.Value,
                ServiceId = service.Id
            });

            // incrementa contagem e encerra se necessário
            var trackedService = await _context.Services.FindAsync(service.Id);
            if (trackedService is not null)
            {
                if (trackedService.Value <= 0) trackedService.Value = cost; // corrige serviços antigos com valor 0
                trackedService.QuantProfessionals += 1;
                if (trackedService.QuantProfessionals >= 4)
                    trackedService.Status = "encerrado";
            }

            // debita créditos se não for assinante
            if (!hasSubscription && hasCredits)
            {
                var trackedUser = await _context.Users.FindAsync(userId.Value);
                if (trackedUser is not null)
                {
                    trackedUser.Credits -= cost;
                    if (trackedUser.Credits < 0) trackedUser.Credits = 0;
                }
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // retorna já com telefone liberado
        var updatedService = await _serviceService.GetByIdWithClientAsync(id);
        return Ok(ProjectService(updatedService!, includePhone: true));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Service>> Create(Service request)
    {
        // força o usuário autenticado como dono do serviço
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized();

        request.UserId = userId;
        request.ClientId = userId;

        var created = await _serviceService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize]
    [HttpGet("search")]
    public async Task<ActionResult<object>> SearchByCategory([FromQuery] List<string>? categories, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1) return BadRequest("page e pageSize devem ser maiores que zero.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var (items, total) = await _serviceService.GetByCategoryPagedAsync(categories, page, pageSize, excludeProfessionalId: userId.Value);
        var totalPages = (int)Math.Ceiling((double)total / pageSize);

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages,
            items = items.Select(s => ProjectService(s, includePhone: false))
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Service>> Update(Guid id, Service request)
    {
        var updated = await _serviceService.UpdateAsync(id, request);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var removed = await _serviceService.DeleteAsync(id);
        if (!removed) return NotFound();
        return NoContent();
    }

    private static object ProjectService(Service service, bool includePhone)
    {
        return new
        {
            service.Id,
            service.Title,
            service.Description,
            service.Category,
            service.Urgency,
            service.Status,
            service.Address,
            service.CreatedAt,
            service.UpdatedAt,
            service.Value,
            service.QuantProfessionals,
            clientId = service.ClientId,
            clientEmail = includePhone ? service.Client?.Email : null,
            clientPhone = includePhone ? service.Client?.Phone : null
        };
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(sub, out var id))
            return id;

        return null;
    }
}


