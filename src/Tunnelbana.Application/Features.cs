using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
namespace Tunnelbana.Application;

public sealed record GraphPayload(byte[] Json, string ETag);

// ---- Query: pre-serialized 3D graph, cache-aside ----
public sealed record GetNetworkGraphQuery : IRequest<GraphPayload>;
public sealed class GetNetworkGraphQueryHandler(IDistributedCache cache, INetworkGraphBuilder builder) : IRequestHandler<GetNetworkGraphQuery, GraphPayload>
{
    public const string JsonKey = "metro:graph:json", EtagKey = "metro:graph:etag";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<GraphPayload> Handle(GetNetworkGraphQuery q, CancellationToken ct)
    {
        var json = await cache.GetAsync(JsonKey, ct);
        var etag = await cache.GetStringAsync(EtagKey, ct);
        if (json is not null && etag is not null) return new(json, etag);

        var graph = await builder.BuildAsync(ct);
        json = JsonSerializer.SerializeToUtf8Bytes(graph, Json);
        etag = $"\"{Convert.ToHexString(SHA256.HashData(json))[..16]}\"";
        var opt = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) };
        // do not cache an empty graph (ETL not finished yet)
        if (graph.Nodes.Count > 0)
        {
            await cache.SetAsync(JsonKey, json, opt, ct);
            await cache.SetStringAsync(EtagKey, etag, opt, ct);
        }
        return new(json, etag);
    }
}

// ---- Command: invalidate after ETL ----
public sealed record InvalidateNetworkCacheCommand : IRequest;
public sealed class InvalidateNetworkCacheHandler(IDistributedCache cache) : IRequestHandler<InvalidateNetworkCacheCommand>
{
    public async Task Handle(InvalidateNetworkCacheCommand c, CancellationToken ct)
    {
        await cache.RemoveAsync(GetNetworkGraphQueryHandler.JsonKey, ct);
        await cache.RemoveAsync(GetNetworkGraphQueryHandler.EtagKey, ct);
    }
}

// ---- Query: spatial ----
public sealed record GetStationsNearbyQuery(double Lat, double Lon, double RadiusM) : IRequest<IReadOnlyList<StationDto>>;
public sealed class GetStationsNearbyHandler(IMetroReadRepository repo) : IRequestHandler<GetStationsNearbyQuery, IReadOnlyList<StationDto>>
{
    public async Task<IReadOnlyList<StationDto>> Handle(GetStationsNearbyQuery q, CancellationToken ct) =>
        (await repo.GetStationsWithinAsync(q.Lat, q.Lon, Math.Clamp(q.RadiusM, 1, 20_000), ct))
        .Select(s => new StationDto(s.Id, s.Name, s.Location.Latitude, s.Location.Longitude, s.Location.ElevationM, s.DepthBelowGroundM, s.Depth?.PlatformAltitudeM)).ToList();
}

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        s.AddSingleton<ILocalProjection, LocalProjection>();
        s.AddScoped<INetworkGraphBuilder, NetworkGraphBuilder>();
        return s;
    }
}
