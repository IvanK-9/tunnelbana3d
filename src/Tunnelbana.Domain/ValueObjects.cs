namespace Tunnelbana.Domain;

public sealed class DomainException(string message) : Exception(message);

/// <summary>WGS84 coordinate. ElevationM = metres above sea level (ground or platform, depending on context).</summary>
public sealed record GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }
    public double? ElevationM { get; }

    public GeoCoordinate(double latitude, double longitude, double? elevationM = null)
    {
        if (latitude is < -90 or > 90) throw new DomainException($"Latitude {latitude} out of range.");
        if (longitude is < -180 or > 180) throw new DomainException($"Longitude {longitude} out of range.");
        if (elevationM is { } e && (double.IsNaN(e) || e is < -500 or > 9000)) throw new DomainException($"Elevation {e} implausible.");
        (Latitude, Longitude, ElevationM) = (latitude, longitude, elevationM);
    }

    public GeoCoordinate WithElevation(double? elevationM) => new(Latitude, Longitude, elevationM);

    public double DistanceMetersTo(GeoCoordinate o)
    {
        const double R = 6_371_000;
        double dLat = (o.Latitude - Latitude) * Math.PI / 180, dLon = (o.Longitude - Longitude) * Math.PI / 180;
        double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                   Math.Cos(Latitude * Math.PI / 180) * Math.Cos(o.Latitude * Math.PI / 180) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}

/// <summary>Vertical profile of a station: ground height and how deep the platform sits beneath it.</summary>
public sealed record DepthProfile
{
    public double GroundElevationM { get; }
    public double DepthBelowGroundM { get; }
    public double PlatformAltitudeM => GroundElevationM - DepthBelowGroundM;

    public DepthProfile(double groundElevationM, double depthBelowGroundM)
    {
        if (depthBelowGroundM is < 0 or > 120) throw new DomainException($"Depth {depthBelowGroundM} m outside 0..120 m.");
        (GroundElevationM, DepthBelowGroundM) = (groundElevationM, depthBelowGroundM);
    }
}
