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
        // core
        new("T-Centralen",     59.3317, 18.0594, 25),
        new("Kungsträdgården", 59.3303, 18.0735, 34),
        new("Rådhuset",        59.3325, 18.0415, 26),
        new("Fridhemsplan",    59.3335, 18.0319, 25),
        new("Hötorget",        59.3358, 18.0636, 25),
        new("Gamla stan",      59.3230, 18.0670, 18),
        new("Slussen",         59.3203, 18.0720, 12),
        // Green line, north & west branch (coordinates approximate; ETL replaces them with SL data when the name matches)
        new("Rådmansgatan",    59.3405, 18.0589, 20),
        new("Odenplan",        59.3430, 18.0462, 20),
        new("S:t Eriksplan",   59.3394, 18.0366, 12),
        new("Thorildsplan",    59.3317, 18.0152, 3),
        new("Kristineberg",    59.3326, 18.0035, 3),
        new("Alvik",           59.3336, 17.9805, 0),
        new("Stora mossen",    59.3347, 17.9690, 0),
        new("Abrahamsberg",    59.3367, 17.9535, 0),
        new("Brommaplan",      59.3385, 17.9392, 0),
        new("Åkeshov",         59.3430, 17.9232, 0),
        new("Ängbyplan",       59.3465, 17.9068, 0),
        new("Islandstorget",   59.3493, 17.8949, 0),
        new("Blackeberg",      59.3492, 17.8830, 0),
        new("Råcksta",         59.3545, 17.8820, 0),
        new("Vällingby",       59.3639, 17.8706, 5),
        // NOT metro: Spånga is a commuter-rail (pendeltåg) station; shown as a standalone node with no tunnels
        new("Spånga",          59.3836, 17.9031, 0),
    ];
    public static readonly SeedLine[] Lines =
    [
        new("Blue",  "T10/T11",     "#0A71B8", ["Kungsträdgården", "T-Centralen", "Rådhuset", "Fridhemsplan"]),
        new("Green", "T17/T18/T19", "#4FA64A", ["Vällingby", "Råcksta", "Blackeberg", "Islandstorget", "Ängbyplan", "Åkeshov", "Brommaplan",
            "Abrahamsberg", "Stora mossen", "Alvik", "Kristineberg", "Thorildsplan", "Fridhemsplan", "S:t Eriksplan", "Odenplan",
            "Rådmansgatan", "Hötorget", "T-Centralen", "Gamla stan", "Slussen"]),
        new("Red",   "T13/T14",     "#E3342F", ["T-Centralen", "Gamla stan", "Slussen"]),
    ];
}
