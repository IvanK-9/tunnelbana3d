using Microsoft.AspNetCore.SignalR;
using Tunnelbana.Application;
namespace Tunnelbana.Api;

public interface IVehicleClient { Task Vehicles(IReadOnlyList<VehiclePositionDto> batch); }
public sealed class VehicleHub : Hub<IVehicleClient> { }

/// <summary>Simulated fleet (swap for GTFS-RT VehiclePositions): trains shuttle along each line's edge chain;
/// position = cubic Bezier at arc fraction t, heading = normalised derivative.</summary>
public sealed class VehicleSimulator(IServiceScopeFactory sf, IHubContext<VehicleHub, IVehicleClient> hub, ILogger<VehicleSimulator> log) : BackgroundService
{
    private sealed class Train(string id, LineDto line, List<EdgeDto> chain, int edge, int dir)
    { public string Id = id; public LineDto Line = line; public List<EdgeDto> Chain = chain; public int Edge = edge; public double Dist; public int Dir = dir; }

    private const double SpeedMps = 14; // ~50 km/h average incl. dwell

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        List<Train> fleet = [];
        var nextRebuild = DateTime.MinValue; var last = DateTime.UtcNow;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                if (DateTime.UtcNow >= nextRebuild) { fleet = await BuildFleet(ct); nextRebuild = DateTime.UtcNow.AddSeconds(fleet.Count == 0 ? 15 : 600); }
                var now = DateTime.UtcNow; double dt = (now - last).TotalSeconds; last = now;
                var batch = fleet.Select(t => Step(t, dt)).ToList();
                if (batch.Count > 0) await hub.Clients.All.Vehicles(batch);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Simulator tick failed"); nextRebuild = DateTime.UtcNow.AddSeconds(15); }
        }
    }

    private async Task<List<Train>> BuildFleet(CancellationToken ct)
    {
        await using var scope = sf.CreateAsyncScope();
        var g = await scope.ServiceProvider.GetRequiredService<INetworkGraphBuilder>().BuildAsync(ct);
        var fleet = new List<Train>();
        foreach (var line in g.Lines)
        {
            var chain = g.Edges.Where(e => e.LineId == line.Id).ToList(); // ordered by Sequence
            if (chain.Count == 0) continue;
            fleet.Add(new Train($"{line.Designation}-A", line, chain, 0, 1));
            fleet.Add(new Train($"{line.Designation}-B", line, chain, chain.Count - 1, -1));
        }
        return fleet;
    }

    private static VehiclePositionDto Step(Train t, double dt)
    {
        t.Dist += SpeedMps * dt;
        var e = t.Chain[t.Edge];
        while (t.Dist >= e.LengthM)
        {
            t.Dist -= e.LengthM; t.Edge += t.Dir;
            if (t.Edge < 0 || t.Edge >= t.Chain.Count) { t.Dir = -t.Dir; t.Edge = Math.Clamp(t.Edge, 0, t.Chain.Count - 1); }
            e = t.Chain[t.Edge];
        }
        double f = Math.Clamp(t.Dist / Math.Max(e.LengthM, 1), 0, 1);
        double u = t.Dir > 0 ? f : 1 - f;
        var pos = Bezier.Eval(e.ControlPoints, u); var tan = Bezier.Tangent(e.ControlPoints, u);
        if (t.Dir < 0) tan = new(-tan.X, -tan.Y, -tan.Z);
        return new(t.Id, t.Line.Id, e.Id, u, pos, tan, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
