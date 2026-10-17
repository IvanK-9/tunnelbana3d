namespace Tunnelbana.Application;

/// <summary>Local ENU frame: X = east, Y = up (altitude m * exaggeration), Z = south (-north). Three.js compatible.</summary>
public sealed record Vec3(double X, double Y, double Z);
public sealed record CoordinateFrameDto(string Description, double OriginLatitude, double OriginLongitude, string Axes, string Units, double VerticalExaggeration);
public sealed record NodeDto(Guid Id, string Name, Vec3 Position, double GroundY, double DepthM, double Latitude, double Longitude);
public sealed record LineDto(Guid Id, string Designation, string Name, string ColourHex);
/// <summary>SplineType "cubic-bezier": ControlPoints = [P0, C1, C2, P3] (Catmull-Rom-derived, tangent-continuous across a line).</summary>
public sealed record EdgeDto(Guid Id, Guid LineId, Guid FromNodeId, Guid ToNodeId, string SplineType, IReadOnlyList<Vec3> ControlPoints, double LengthM);
public sealed record NetworkGraphDto(string SchemaVersion, DateTimeOffset GeneratedAt, CoordinateFrameDto Frame,
    IReadOnlyList<NodeDto> Nodes, IReadOnlyList<LineDto> Lines, IReadOnlyList<EdgeDto> Edges);
public sealed record StationDto(Guid Id, string Name, double Latitude, double Longitude, double? GroundElevationM, double? DepthM, double? PlatformAltitudeM);
public sealed record VehiclePositionDto(string VehicleId, Guid LineId, Guid EdgeId, double T, Vec3 Position, Vec3 Tangent, long Ts);
