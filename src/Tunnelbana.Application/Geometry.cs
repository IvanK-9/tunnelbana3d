using Microsoft.Extensions.Options;
using Tunnelbana.Domain;
namespace Tunnelbana.Application;

public sealed class LocalProjection(IOptions<ProjectionOptions> o) : ILocalProjection
{
    private readonly ProjectionOptions _o = o.Value;
    public Vec3 Project(GeoCoordinate g)
    {
        double x = (g.Longitude - _o.OriginLongitude) * Math.Cos(_o.OriginLatitude * Math.PI / 180) * 111_320;
        double z = -(g.Latitude - _o.OriginLatitude) * 110_574;
        return new Vec3(x, (g.ElevationM ?? 0) * _o.VerticalExaggeration, z);
    }
}

public static class Bezier
{
    public static Vec3 Eval(IReadOnlyList<Vec3> p, double t)
    {
        double u = 1 - t, a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
        return new(a * p[0].X + b * p[1].X + c * p[2].X + d * p[3].X,
                   a * p[0].Y + b * p[1].Y + c * p[2].Y + d * p[3].Y,
                   a * p[0].Z + b * p[1].Z + c * p[2].Z + d * p[3].Z);
    }
    public static Vec3 Tangent(IReadOnlyList<Vec3> p, double t)
    {
        double u = 1 - t, a = 3 * u * u, b = 6 * u * t, c = 3 * t * t;
        var v = new Vec3(a * (p[1].X - p[0].X) + b * (p[2].X - p[1].X) + c * (p[3].X - p[2].X),
                         a * (p[1].Y - p[0].Y) + b * (p[2].Y - p[1].Y) + c * (p[3].Y - p[2].Y),
                         a * (p[1].Z - p[0].Z) + b * (p[2].Z - p[1].Z) + c * (p[3].Z - p[2].Z));
        double len = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return len < 1e-9 ? new(0, 0, 0) : new(v.X / len, v.Y / len, v.Z / len);
    }
    public static double Length(IReadOnlyList<Vec3> p, int n = 24)
    {
        double len = 0; var prev = Eval(p, 0);
        for (int i = 1; i <= n; i++)
        {
            var q = Eval(p, (double)i / n);
            len += Math.Sqrt(Math.Pow(q.X - prev.X, 2) + Math.Pow(q.Y - prev.Y, 2) + Math.Pow(q.Z - prev.Z, 2));
            prev = q;
        }
        return len;
    }
}
