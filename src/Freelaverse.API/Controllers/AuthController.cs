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

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly IProfessionalAreaService _professionalAreaService;
    private readonly IUserProfessionalAreaService _userProfessionalAreaService;
    private readonly AppDbContext _db;

    public AuthController(
        IUserService userService,
        IConfiguration configuration,
        IProfessionalAreaService professionalAreaService,
        IUserProfessionalAreaService userProfessionalAreaService,
        AppDbContext db)
    {
        _userService = userService;
        _configuration = configuration;
        _professionalAreaService = professionalAreaService;
        _userProfessionalAreaService = userProfessionalAreaService;
        _db = db;
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
        var result = new
        {
            userName = user.UserName,
            email = user.Email,
            userType = user.UserType,
            profileImageUrl = user.ProfileImageUrl,
            phone = user.Phone,
            clientServices = user.ClientServices?
                .Select(s => new
                {
                    title = s.Title,
                    description = s.Description,
                    category = s.Category,
                    urgency = s.Urgency,
                    status = s.Status,
                    address = s.Address,
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
                        title = ps.Service.Title,
                        description = ps.Service.Description,
                        category = ps.Service.Category,
                        urgency = ps.Service.Urgency,
                        status = ps.Service.Status,
                        address = ps.Service.Address
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
                .ToList() ?? new List<object>()
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

            var token = GenerateJwtToken(created);
            return Ok(new { token, user = new { created.Id, created.Email, created.UserName, created.UserType, created.ProfileImageUrl } });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        var user = await _userService.GetByEmailAndPasswordAsync(request.Email, request.Password);
        if (user is null)
        {
            return Unauthorized();
        }

        var token = GenerateJwtToken(user);
        return Ok(new { token, user = new { user.Id, user.Email, user.UserName, user.UserType, user.ProfileImageUrl } });
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
}


