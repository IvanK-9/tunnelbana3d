namespace Tunnelbana.Domain;

public sealed class Station
{
    private Station() { } // EF
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? SiteId { get; private set; }                 // Trafiklab / SL site id
    public GeoCoordinate Location { get; private set; } = null!; // surface location; ElevationM = ground height
    public double? DepthBelowGroundM { get; private set; }

    public DepthProfile? Depth => Location.ElevationM is { } g && DepthBelowGroundM is { } d ? new(g, d) : null;
    /// <summary>Platform position in 3D (lat/lon at surface, altitude = platform altitude).</summary>
    public GeoCoordinate PlatformLocation => Location.WithElevation(Depth?.PlatformAltitudeM ?? Location.ElevationM ?? 0);

    public static Station Create(string name, GeoCoordinate location, string? siteId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Station name required.");
        return new Station { Id = Guid.NewGuid(), Name = name.Trim(), Location = location, SiteId = siteId };
    }

    public void Relocate(GeoCoordinate location, string? siteId)
    {
        Location = location.WithElevation(Location.ElevationM);
        SiteId = siteId ?? SiteId;
    }

    public void ApplyGroundElevation(double elevationM) => Location = Location.WithElevation(elevationM);

    public void ApplyDepth(double depthBelowGroundM)
    {
        if (Location.ElevationM is not { } g) throw new DomainException("Ground elevation must be applied before depth.");
        _ = new DepthProfile(g, depthBelowGroundM); // invariant check
        DepthBelowGroundM = depthBelowGroundM;
    }
}

public sealed class LineStop
{
    private LineStop() { }
    public LineStop(Guid stationId, int sequence) => (StationId, Sequence) = (stationId, sequence);
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StationId { get; private set; }
    public int Sequence { get; private set; }
}

public sealed class Line
{
    private readonly List<LineStop> _stops = [];
    private Line() { }
    public Guid Id { get; private set; }
    public string Designation { get; private set; } = ""; // "Blue"
    public string Name { get; private set; } = "";        // "T10/T11"
    public string ColourHex { get; private set; } = "#000000";
    public IReadOnlyList<LineStop> Stops => _stops;

    public static Line Create(string designation, string name, string colourHex) =>
        new() { Id = Guid.NewGuid(), Designation = designation, Name = name, ColourHex = colourHex };

    public void SetStops(IEnumerable<Guid> orderedStationIds)
    {
        var ids = orderedStationIds.ToList();
        if (ids.Count < 2) throw new DomainException("A line needs at least two stops.");
        if (ids.Distinct().Count() != ids.Count) throw new DomainException("Duplicate stop on line.");
        _stops.Clear();
        _stops.AddRange(ids.Select((id, i) => new LineStop(id, i)));
    }
}

/// <summary>Directed tunnel between consecutive stops of one line. Path = [from, c1, c2, to] cubic Bezier (geographic space, altitude in m).</summary>
public sealed class TunnelSegment
{
    private TunnelSegment() { }
    public Guid Id { get; private set; }
    public Guid LineId { get; private set; }
    public Guid FromStationId { get; private set; }
    public Guid ToStationId { get; private set; }
    public int Sequence { get; private set; }
    public GeoCoordinate[] ControlPoints { get; private set; } = [];

    public static TunnelSegment Create(Guid lineId, Guid from, Guid to, int sequence, IEnumerable<GeoCoordinate> bezier)
    {
        var pts = bezier.ToArray();
        if (pts.Length != 4) throw new DomainException("Cubic Bezier needs exactly 4 control points.");
        if (from == to) throw new DomainException("Segment endpoints must differ.");
        return new TunnelSegment { Id = Guid.NewGuid(), LineId = lineId, FromStationId = from, ToStationId = to, Sequence = sequence, ControlPoints = pts };
    }
}
