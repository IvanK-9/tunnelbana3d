using Microsoft.Extensions.Options;
namespace Tunnelbana.Application;

public sealed class NetworkGraphBuilder(IMetroReadRepository repo, ILocalProjection proj, IOptions<ProjectionOptions> po) : INetworkGraphBuilder
{
    public async Task<NetworkGraphDto> BuildAsync(CancellationToken ct)
    {
        var s = await repo.GetSnapshotAsync(ct);
        var nodes = s.Stations.OrderBy(x => x.Name).Select(x => new NodeDto(
            x.Id, x.Name, proj.Project(x.PlatformLocation), proj.Project(x.Location).Y, x.DepthBelowGroundM ?? 0,
            x.Location.Latitude, x.Location.Longitude)).ToList();
        var lines = s.Lines.Select(l => new LineDto(l.Id, l.Designation, l.Name, l.ColourHex)).ToList();
        var edges = s.Segments.OrderBy(x => x.LineId).ThenBy(x => x.Sequence).Select(g =>
        {
            var cps = g.ControlPoints.Select(proj.Project).ToArray();
            return new EdgeDto(g.Id, g.LineId, g.FromStationId, g.ToStationId, "cubic-bezier", cps, Math.Round(Bezier.Length(cps), 1));
        }).ToList();
        var o = po.Value;
        return new NetworkGraphDto("1.0", DateTimeOffset.UtcNow,
            new CoordinateFrameDto("Local ENU around origin", o.OriginLatitude, o.OriginLongitude, "X=east, Y=up, Z=south", "metres", o.VerticalExaggeration),
            nodes, lines, edges);
    }
}
