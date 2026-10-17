using MediatR;
using Tunnelbana.Api;
using Tunnelbana.Application;
using Tunnelbana.Infrastructure;

var b = WebApplication.CreateBuilder(args);
b.Services.AddApplication().AddInfrastructure(b.Configuration);
b.Services.AddSignalR();
b.Services.AddHostedService<VehicleSimulator>();
b.Services.AddOpenApi();
b.Services.AddHealthChecks();
b.Services.AddResponseCompression();
b.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(b.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = b.Build();
for (var attempt = 1; ; attempt++)   // DB may still be restarting after first-time init
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MetroDbContext>().Database.EnsureCreatedAsync();
        break;
    }
    catch (Exception ex) when (attempt < 30)
    {
        app.Logger.LogWarning("Database not ready (attempt {A}/30): {M}", attempt, ex.Message);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
app.UseResponseCompression();
app.UseCors();
app.MapOpenApi();                 // /openapi/v1.json
app.MapHealthChecks("/health");
app.MapHub<VehicleHub>("/hubs/vehicles");

var api = app.MapGroup("/api/v1");

api.MapGet("/network/graph", async (IMediator m, HttpRequest req, HttpResponse res, CancellationToken ct) =>
{
    var p = await m.Send(new GetNetworkGraphQuery(), ct);
    res.Headers.ETag = p.ETag; res.Headers.CacheControl = "public, max-age=60";
    if (req.Headers.IfNoneMatch == p.ETag) return Results.StatusCode(304);
    return Results.Bytes(p.Json, "application/json");           // pre-serialized: no per-request serialization
}).WithName("GetNetworkGraph").Produces<NetworkGraphDto>(200, "application/json").Produces(304);

api.MapGet("/stations/nearby", async (double lat, double lon, double radiusM, IMediator m, CancellationToken ct) =>
    Results.Ok(await m.Send(new GetStationsNearbyQuery(lat, lon, radiusM), ct)))
    .WithName("GetStationsNearby").Produces<IReadOnlyList<StationDto>>(200);

app.Run();
