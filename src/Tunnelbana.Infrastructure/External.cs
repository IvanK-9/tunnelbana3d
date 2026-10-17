using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Tunnelbana.Domain;
namespace Tunnelbana.Infrastructure;

public sealed record SlSite([property: JsonPropertyName("id")] int Id, [property: JsonPropertyName("name")] string Name,
                            [property: JsonPropertyName("lat")] double Lat, [property: JsonPropertyName("lon")] double Lon);

public interface ISlSitesClient { Task<IReadOnlyList<SlSite>> GetSitesAsync(CancellationToken ct); }
public interface IElevationProvider { Task<double?[]> LookupAsync(IReadOnlyList<GeoCoordinate> points, CancellationToken ct); }

/// <summary>Trafiklab SL Transport API (open, no key): sites incl. coordinates.</summary>
public sealed class SlSitesClient(HttpClient http) : ISlSitesClient
{
    public async Task<IReadOnlyList<SlSite>> GetSitesAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<SlSite>>("v1/sites", ct) ?? [];
}

/// <summary>Open-Elevation (SRTM). Swap for a Lantmäteriet Markhöjd provider in production by implementing IElevationProvider.</summary>
public sealed class OpenElevationProvider(HttpClient http) : IElevationProvider
{
    private sealed record Res(List<Item> Results);
    private sealed record Item(double Elevation);
    public async Task<double?[]> LookupAsync(IReadOnlyList<GeoCoordinate> pts, CancellationToken ct)
    {
        var body = new { locations = pts.Select(p => new { latitude = p.Latitude, longitude = p.Longitude }) };
        using var resp = await http.PostAsJsonAsync("api/v1/lookup", body, ct);
        resp.EnsureSuccessStatusCode();
        var r = await resp.Content.ReadFromJsonAsync<Res>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        if (r is null || r.Results.Count != pts.Count) throw new InvalidOperationException("Elevation response size mismatch.");
        return r.Results.Select(x => (double?)x.Elevation).ToArray();
    }
}

public static class ResiliencePipelines
{
    /// Order matters: rate limiter -> retry (exp. backoff + jitter) -> circuit breaker -> per-attempt timeout.
    public static IHttpResiliencePipelineBuilder AddExternalApiResilience(this IHttpClientBuilder b, string name, int permitsPerSecond = 5) =>
        b.AddResilienceHandler(name, p =>
        {
            p.AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            { PermitLimit = permitsPerSecond, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 4, QueueLimit = 50 }));
            p.AddRetry(new HttpRetryStrategyOptions
            { MaxRetryAttempts = 5, Delay = TimeSpan.FromSeconds(1), BackoffType = DelayBackoffType.Exponential, UseJitter = true });
            p.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            { SamplingDuration = TimeSpan.FromSeconds(30), FailureRatio = 0.5, MinimumThroughput = 4, BreakDuration = TimeSpan.FromSeconds(30) });
            p.AddTimeout(TimeSpan.FromSeconds(15));
        });
}
