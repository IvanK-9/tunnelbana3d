namespace Tunnelbana.Domain;

/// <summary>Uniform Catmull-Rom through station platforms, converted to per-segment cubic Bezier controls:
/// C1 = P1 + (P2-P0)/6,  C2 = P2 - (P3-P1)/6. Affine-invariant, so valid in lat/lon/alt space at city scale.</summary>
public static class SplineBuilder
{
    public static IReadOnlyList<(GeoCoordinate C1, GeoCoordinate C2)> Build(IReadOnlyList<GeoCoordinate> p)
    {
        var res = new List<(GeoCoordinate, GeoCoordinate)>();
        for (int i = 0; i < p.Count - 1; i++)
        {
            var p0 = p[Math.Max(i - 1, 0)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Math.Min(i + 2, p.Count - 1)];
            res.Add((Offset(p1, p2, p0, 1.0 / 6), Offset(p2, p3, p1, -1.0 / 6)));
        }
        return res;
    }

    private static GeoCoordinate Offset(GeoCoordinate origin, GeoCoordinate a, GeoCoordinate b, double k) => new(
        origin.Latitude + (a.Latitude - b.Latitude) * k,
        origin.Longitude + (a.Longitude - b.Longitude) * k,
        (origin.ElevationM ?? 0) + ((a.ElevationM ?? 0) - (b.ElevationM ?? 0)) * k);
}
