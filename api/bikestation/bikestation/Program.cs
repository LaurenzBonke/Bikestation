using System.Text.Json.Serialization;
using bikestation.Data;
using bikestation.Options;
using bikestation.Services;
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

builder.Services.AddScoped<SensorDataService>();
builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<AlertService>();

// Erlaubt dem Web-Dashboard (anderer Port) Zugriff auf die API
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// Legt die SQLite-Datei samt Tabellen und den 3 Slots beim ersten Start an.
// Hinweis: Bei Änderungen am Datenmodell die .db-Datei löschen (bis wir auf Migrations umstellen).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BikestationDbContext>().Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();

app.Run();
