using Tunnelbana.Domain;
namespace Tunnelbana.Application;

public sealed record MetroSnapshot(IReadOnlyList<Station> Stations, IReadOnlyList<Line> Lines, IReadOnlyList<TunnelSegment> Segments);

public interface IMetroReadRepository
{
    Task<MetroSnapshot> GetSnapshotAsync(CancellationToken ct);
    /// <summary>PostGIS ST_DWithin on geography.</summary>
    Task<IReadOnlyList<Station>> GetStationsWithinAsync(double lat, double lon, double radiusM, CancellationToken ct);
}

public interface ILocalProjection { Vec3 Project(GeoCoordinate g); }
public interface INetworkGraphBuilder { Task<NetworkGraphDto> BuildAsync(CancellationToken ct); }

public sealed class ProjectionOptions
{
    public double OriginLatitude { get; set; } = 59.3317;   // T-Centralen
    public double OriginLongitude { get; set; } = 18.0594;
    public double VerticalExaggeration { get; set; } = 1.0;
}
