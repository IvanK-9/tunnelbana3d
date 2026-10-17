using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tunnelbana.Application;
using Tunnelbana.Domain;
namespace Tunnelbana.Infrastructure;

public sealed class EtlOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);
    public double DefaultGroundElevationM { get; set; } = 15; // only if DEM lookup fails AND no prior value exists
}

public sealed class MetroEtlWorker(IServiceScopeFactory sf, IOptions<EtlOptions> o, ILogger<MetroEtlWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(o.Value.Interval);
        do
        {
            try
            {
                await using var scope = sf.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MetroEtlPipeline>().RunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { log.LogError(ex, "ETL run failed; will retry at next interval."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

/// <summary>Extract (SL sites + DEM) -> Transform (match, normalise, depth, spline) -> Load (PostGIS, idempotent, transactional) -> invalidate cache.</summary>
public sealed class MetroEtlPipeline(MetroDbContext db, ISlSitesClient sl, IElevationProvider dem, IMediator mediator,
    IOptions<EtlOptions> opt, ILogger<MetroEtlPipeline> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await db.Database.EnsureCreatedAsync(ct); // use EF migrations in production
        log.LogInformation("ETL started");

        // EXTRACT
        IReadOnlyList<SlSite> sites = [];
        try { sites = await sl.GetSitesAsync(ct); }
        catch (Exception ex) { log.LogWarning(ex, "SL sites unavailable; using seed coordinates."); }

        var resolved = SeedCatalog.Stations.Select(seed =>
        {
            var sc = new GeoCoordinate(seed.Lat, seed.Lon);
            var m = sites.Where(x => string.Equals(x.Name, seed.Name, StringComparison.OrdinalIgnoreCase))
                .Select(x => (Site: x, D: sc.DistanceMetersTo(new GeoCoordinate(x.Lat, x.Lon)))).Where(t => t.D < 500)
                .OrderBy(t => t.D).Select(t => t.Site).FirstOrDefault();
            return (Seed: seed, Loc: m is null ? sc : new GeoCoordinate(m.Lat, m.Lon), SiteId: m?.Id.ToString());
        }).ToList();

        double?[] elev;
        try { elev = await dem.LookupAsync(resolved.Select(r => r.Loc).ToList(), ct); }
        catch (Exception ex) { log.LogWarning(ex, "DEM lookup failed; falling back to stored/default elevations."); elev = new double?[resolved.Count]; }

        // TRANSFORM + LOAD (single transaction => atomic network swap)
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var stations = await db.Stations.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase, ct);
        for (int i = 0; i < resolved.Count; i++)
        {
            var (seed, loc, siteId) = resolved[i];
            if (!stations.TryGetValue(seed.Name, out var st))
            {
                st = Station.Create(seed.Name, loc, siteId); db.Stations.Add(st); stations[seed.Name] = st;
            }
            else st.Relocate(loc, siteId);
            st.ApplyGroundElevation(elev[i] ?? st.Location.ElevationM ?? opt.Value.DefaultGroundElevationM);
            st.ApplyDepth(seed.DepthM);
        }
        await db.SaveChangesAsync(ct);

        // Lines are rebuilt from scratch on every run (avoids key conflicts); inside the transaction, so atomic
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tunnel_segments; DELETE FROM line_stops; DELETE FROM lines;", ct);
        var lines = new Dictionary<string, Line>();
        foreach (var def in SeedCatalog.Lines)
        {
            var ids = def.Stops.Select(n => stations[n].Id).ToList();
            var line = Line.Create(def.Designation, def.Name, def.Colour);
            line.SetStops(ids);
            db.Lines.Add(line); lines[def.Designation] = line;

            var platforms = def.Stops.Select(n => stations[n].PlatformLocation).ToList();
            var splines = SplineBuilder.Build(platforms);
            for (int k = 0; k < splines.Count; k++)
                db.Segments.Add(TunnelSegment.Create(line.Id, ids[k], ids[k + 1], k, [platforms[k], splines[k].C1, splines[k].C2, platforms[k + 1]]));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await mediator.Send(new InvalidateNetworkCacheCommand(), ct);
        log.LogInformation("ETL finished: {S} stations, {L} lines", stations.Count, lines.Count);
    }
}
