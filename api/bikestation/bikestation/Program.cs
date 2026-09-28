using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using bikestation;
using bikestation.Data;
using bikestation.Options;
using bikestation.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    // Enums als Text ausgeben ("Occupied" statt 2) – einfacher fürs Frontend
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<BikestationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Bikestation")));

builder.Services.Configure<OccupancyOptions>(builder.Configuration.GetSection(OccupancyOptions.SectionName));

// API-Key für ESP32 und KI-Dienst
var deviceSection = builder.Configuration.GetSection(DeviceOptions.SectionName);
builder.Services.Configure<DeviceOptions>(deviceSection);
if ((deviceSection.Get<DeviceOptions>()?.ApiKey.Length ?? 0) < 16)
{
    throw new InvalidOperationException(
        "Devices:ApiKey fehlt oder ist kürzer als 16 Zeichen. Per Umgebungsvariable Devices__ApiKey setzen.");
}

builder.Services.AddScoped<SensorDataService>();
builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AnomalyService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<DemoDataService>();

// JWT-Authentifizierung für Admin-Funktionen
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key fehlt oder ist kürzer als 32 Zeichen. Per Umgebungsvariable Jwt__Key setzen.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claim-Namen wie im Token lassen (z. B. "unique_name")
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

// Schutz gegen Passwort-Raten: max. 5 Login-Versuche pro Minute und IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

// Erlaubt dem Web-Dashboard (anderer Port) Zugriff auf die API
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// Legt die SQLite-Datei samt Tabellen und den 3 Slots an und erkennt veraltete Datenbanken
DatabaseInitializer.Initialize(app);

// Admin anlegen statt Server starten: dotnet run -- create-admin <benutzername>
if (args.Length > 0 && args[0] == "create-admin")
{
    return await CreateAdminCommand.RunAsync(app.Services, args);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
// Keine HTTPS-Umleitung: Die ESP32 senden per HTTP im lokalen Netz und würden einer Umleitung nicht folgen.
// HTTPS kann später ein Reverse Proxy (z. B. nginx) auf dem Raspberry Pi übernehmen.

// Grundlegende Sicherheits-Header für alle Antworten
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

// Liefert das gebaute React-Dashboard aus wwwroot aus (index.html unter "/"),
// damit Frontend und API über denselben Server und Port erreichbar sind
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
return 0;
