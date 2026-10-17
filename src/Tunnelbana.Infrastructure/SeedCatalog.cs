namespace Tunnelbana.Infrastructure;

public sealed record SeedStation(string Name, double Lat, double Lon, double DepthM);
public sealed record SeedLine(string Designation, string Name, string Colour, string[] Stops);

/// <summary>
/// Curated bootstrap catalogue. IMPORTANT: DepthM values are INDICATIVE PLACEHOLDERS (platform depth below street level, m).
/// Replace with authoritative data (Region Stockholm / Trafikverket / municipal as-built drawings) in the same ETL stage.
/// Extend Stations/Lines to cover the full network; the pipeline is data-driven.
/// </summary>
public static class SeedCatalog
{
    public static readonly SeedStation[] Stations =
    [
        new("T-Centralen",     59.3317, 18.0594, 25),
        new("Kungsträdgården", 59.3303, 18.0735, 34),
        new("Rådhuset",        59.3325, 18.0415, 26),
        new("Fridhemsplan",    59.3335, 18.0319, 25),
        new("Hötorget",        59.3358, 18.0636, 25),
        new("Gamla stan",      59.3230, 18.0670, 18),
        new("Slussen",         59.3203, 18.0720, 12),
    ];
    public static readonly SeedLine[] Lines =
    [
        new("Blue",  "T10/T11",     "#0A71B8", ["Kungsträdgården", "T-Centralen", "Rådhuset", "Fridhemsplan"]),
        new("Green", "T17/T18/T19", "#4FA64A", ["Hötorget", "T-Centralen", "Gamla stan", "Slussen"]),
        new("Red",   "T13/T14",     "#E3342F", ["T-Centralen", "Gamla stan", "Slussen"]),
    ];
}
