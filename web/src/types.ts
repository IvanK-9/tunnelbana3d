// Mirrors the backend OpenAPI contract (generate: npx openapi-typescript http://localhost:8080/openapi/v1.json -o src/api.d.ts)
export type Vec3 = { x: number; y: number; z: number };
export type NodeDto = { id: string; name: string; position: Vec3; groundY: number; depthM: number };
export type LineDto = { id: string; designation: string; name: string; colourHex: string };
export type EdgeDto = { id: string; lineId: string; fromNodeId: string; toNodeId: string; splineType: "cubic-bezier"; controlPoints: Vec3[]; lengthM: number };
export type NetworkGraph = { schemaVersion: string; nodes: NodeDto[]; lines: LineDto[]; edges: EdgeDto[] };
export type VehiclePosition = { vehicleId: string; lineId: string; edgeId: string; t: number; position: Vec3; tangent: Vec3; ts: number };
