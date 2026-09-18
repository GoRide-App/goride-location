using GoRide.Location.Data;
using GoRide.Location.Models;
using GoRide.Location.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers + Swagger ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Database (ADO.NET connection factory — see Data/MySqlConnectionFactory.cs) ----
builder.Services.AddScoped<IDbConnectionFactory, MySqlConnectionFactory>();

// ---- HTTP client factory (used by RidePlanService to call ORS) ----
builder.Services.AddHttpClient();

// ---- Options bindings ----
builder.Services.Configure<OrsOptions>(
    builder.Configuration.GetSection(OrsOptions.SectionName));
builder.Services.Configure<ServiceableAreaOptions>(
    builder.Configuration.GetSection(ServiceableAreaOptions.SectionName));

// ---- Business-logic services ----
builder.Services.AddScoped<IRidePlanService, RidePlanService>();
builder.Services.AddScoped<IDriverLocationRepository, DriverLocationRepository>();
builder.Services.AddScoped<IDriverLocationService, DriverLocationService>();

// ---- CORS: allow the Next.js frontend (local dev + Vercel-hosted) to call this API ----
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

var origins = configuredOrigins
    .Where(o => !string.IsNullOrWhiteSpace(o) && !o.Contains("${"))
    .ToList();

if (!origins.Contains("http://localhost:3000", StringComparer.OrdinalIgnoreCase))
{
    origins.Add("http://localhost:3000");
}
if (!origins.Contains("http://127.0.0.1:3000", StringComparer.OrdinalIgnoreCase))
{
    origins.Add("http://127.0.0.1:3000");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ---- Ensure the driver_locations table exists (no migration tool on this service —
// see GoRide.Location.csproj — so schema is created idempotently on startup). A
// failure here is logged but doesn't stop the app: /rides/plan doesn't need this table.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var repository = scope.ServiceProvider.GetRequiredService<IDriverLocationRepository>();
        await repository.EnsureSchemaAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to ensure driver_locations schema exists.");
    }
}

// ---- Swagger UI (dev only — don't expose this publicly in production) ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the generated Program class so integration tests can spin up this app
// in-memory via WebApplicationFactory<Program> later, without any extra setup.
public partial class Program { }

