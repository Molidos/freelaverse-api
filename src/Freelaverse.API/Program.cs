using FreelaverseApi.Data;
using Microsoft.EntityFrameworkCore;
using Freelaverse.Services.Interfaces;
using Freelaverse.Data.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Stripe;
using Freelaverse.API.Hubs;
using Freelaverse.API.Options;
using Freelaverse.API.Services;
using SendGrid;

var builder = WebApplication.CreateBuilder(args);

// CORS 
const string CorsPolicy = "DefaultCors";
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ??
    new[]
    {
        "http://localhost:3000",
        "http://localhost:5002",
        "https://freelaverse.com",
        "https://www.freelaverse.com",
        "https://api.freelaverse.com"
    };
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Freelaverse API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey, // ApiKey garante exibição do campo no Swagger UI
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT no formato: Bearer {token}",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection("Frontend"));

var sendGridApiKey = builder.Configuration.GetSection("SendGrid")["ApiKey"];
if (!string.IsNullOrWhiteSpace(sendGridApiKey))
{
    builder.Services.AddSingleton<ISendGridClient>(_ => new SendGridClient(sendGridApiKey));
    builder.Services.AddScoped<IEmailService, SendGridEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, NoOpEmailService>();
}

// DbContext
builder.Services.AddDbContext<FreelaverseApi.Data.AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IProfessionalAreaService, ProfessionalAreaService>();
builder.Services.AddScoped<IProfessionalServiceService, ProfessionalServiceService>();
builder.Services.AddScoped<IUserProfessionalAreaService, UserProfessionalAreaService>();
builder.Services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();

// Auth/JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!))
        };
    });

// Stripe (prioriza env STRIPE_API_KEY; fallback para Stripe:SecretKey)
var stripeSecret = builder.Configuration["STRIPE_API_KEY"]
                  ?? builder.Configuration.GetValue<string>("Stripe:SecretKey");
if (!string.IsNullOrWhiteSpace(stripeSecret))
{
    StripeConfiguration.ApiKey = stripeSecret;
}

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PaymentsHub>("/hubs/payments");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
