using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Tunnelbana.Application;
using Tunnelbana.Domain;
namespace Tunnelbana.Infrastructure;

public sealed class MetroDbContext(DbContextOptions<MetroDbContext> o) : DbContext(o)
{
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<TunnelSegment> Segments => Set<TunnelSegment>();

    private static readonly GeometryFactory Gf = NtsGeometryServices.Instance.CreateGeometryFactory(4326);
    private static Point ToPoint(GeoCoordinate g) => Gf.CreatePoint(new CoordinateZ(g.Longitude, g.Latitude, g.ElevationM ?? 0));
    private static GeoCoordinate FromPoint(Point p) => new(p.Y, p.X, p.Z);
    private static LineString ToLine(GeoCoordinate[] a) => Gf.CreateLineString(a.Select(g => (Coordinate)new CoordinateZ(g.Longitude, g.Latitude, g.ElevationM ?? 0)).ToArray());
    private static GeoCoordinate[] FromLine(LineString l) => l.Coordinates.Select(c => new GeoCoordinate(c.Y, c.X, c.Z)).ToArray();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasPostgresExtension("postgis");

        mb.Entity<Station>(e =>
        {
            e.ToTable("stations"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Location).HasConversion(new ValueConverter<GeoCoordinate, Point>(g => ToPoint(g), p => FromPoint(p)))
                .HasColumnName("location").HasColumnType("geometry(PointZ,4326)");
            e.Ignore(x => x.Depth); e.Ignore(x => x.PlatformLocation);
        });

        mb.Entity<Line>(e =>
        {
            e.ToTable("lines"); e.HasKey(x => x.Id); e.HasIndex(x => x.Designation).IsUnique();
            e.HasMany(x => x.Stops).WithOne().HasForeignKey("LineId").OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        mb.Entity<LineStop>(s =>
        {
            s.ToTable("line_stops"); s.HasKey(x => x.Id);
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property<Guid>("LineId");
            s.HasIndex("LineId", nameof(LineStop.Sequence)).IsUnique();
        });

        mb.Entity<TunnelSegment>(e =>
        {
            e.ToTable("tunnel_segments"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.LineId, x.Sequence }).IsUnique();
            e.HasOne<Line>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Station>().WithMany().HasForeignKey(x => x.FromStationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Station>().WithMany().HasForeignKey(x => x.ToStationId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ControlPoints).HasConversion(new ValueConverter<GeoCoordinate[], LineString>(a => ToLine(a), l => FromLine(l)),
                new ValueComparer<GeoCoordinate[]>((a, b) => a!.SequenceEqual(b!), a => a.Aggregate(0, (h, x) => HashCode.Combine(h, x)), a => a.ToArray()))
                .HasColumnName("path").HasColumnType("geometry(LineStringZ,4326)");
        });
    }
}

public sealed class MetroReadRepository(MetroDbContext db) : IMetroReadRepository
{
    public async Task<MetroSnapshot> GetSnapshotAsync(CancellationToken ct) => new(
        await db.Stations.AsNoTracking().ToListAsync(ct),
        await db.Lines.AsNoTracking().Include(l => l.Stops).ToListAsync(ct),
        await db.Segments.AsNoTracking().ToListAsync(ct));

    public async Task<IReadOnlyList<Station>> GetStationsWithinAsync(double lat, double lon, double r, CancellationToken ct) =>
        await db.Stations.FromSqlInterpolated($"""
            SELECT * FROM stations
            WHERE ST_DWithin(location::geography, ST_SetSRID(ST_MakePoint({lon},{lat}),4326)::geography, {r})
            """).AsNoTracking().ToListAsync(ct);
}
