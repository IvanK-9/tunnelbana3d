import { Canvas } from "@react-three/fiber";
import { OrbitControls, Html, Line } from "@react-three/drei";
import { useEffect, useMemo, useState } from "react";
import * as THREE from "three";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import type { NetworkGraph, VehiclePosition, EdgeDto } from "./types";

// Presentation only: all coordinates and curves come from the backend (its frame is Three.js-compatible, mapped 1:1).
const v = (p: { x: number; y: number; z: number }) => new THREE.Vector3(p.x, p.y, p.z);

function Tunnel({ edge, colour }: { edge: EdgeDto; colour: string }) {
  const geo = useMemo(() => {
    const [a, b, c, d] = edge.controlPoints.map(v);
    return new THREE.TubeGeometry(new THREE.CubicBezierCurve3(a, b, c, d), 32, 6, 8, false);
  }, [edge]);
  return <mesh geometry={geo}><meshStandardMaterial color={colour} transparent opacity={0.85} /></mesh>;
}

export function App() {
  const [g, setG] = useState<NetworkGraph>();
  const [veh, setVeh] = useState<Record<string, VehiclePosition>>({});

  useEffect(() => {
    const load = () => fetch("/api/v1/network/graph").then(r => r.json()).then((x: NetworkGraph) => x.nodes.length ? setG(x) : setTimeout(load, 3000));
    load();
  }, []);
  useEffect(() => {
    const c = new HubConnectionBuilder().withUrl("/hubs/vehicles").withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
    c.on("Vehicles", (batch: VehiclePosition[]) => setVeh(Object.fromEntries(batch.map(x => [x.vehicleId, x]))));
    c.start().catch(console.error);
    return () => { void c.stop(); };
  }, []);

  const colour = useMemo(() => Object.fromEntries((g?.lines ?? []).map(l => [l.id, l.colourHex])), [g]);
  if (!g) return <div style={{ color: "#fff", padding: 16 }}>Waiting for network (ETL running)…</div>;

  return (
    <Canvas camera={{ position: [0, 2500, 3500], fov: 45, far: 50000 }}>
      <ambientLight intensity={0.7} /><directionalLight position={[2000, 4000, 1000]} intensity={1.2} />
      <gridHelper args={[12000, 60, "#334", "#223"]} />
      {g.edges.map(e => <Tunnel key={e.id} edge={e} colour={colour[e.lineId] ?? "#888"} />)}
      {g.nodes.map(n => (
        <group key={n.id}>
          <mesh position={[n.position.x, n.position.y, n.position.z]}><sphereGeometry args={[14, 16, 16]} /><meshStandardMaterial color="#fff" /></mesh>
          <Line points={[[n.position.x, n.position.y, n.position.z], [n.position.x, n.groundY, n.position.z]]} color="#667" />
          <Html position={[n.position.x, n.groundY, n.position.z]} distanceFactor={1500}>
            <div style={{ color: "#fff", font: "12px sans-serif", whiteSpace: "nowrap" }}>{n.name} (−{n.depthM} m)</div>
          </Html>
        </group>
      ))}
      {Object.values(veh).map(t => (
        <mesh key={t.vehicleId} position={[t.position.x, t.position.y, t.position.z]}>
          <boxGeometry args={[40, 12, 12]} /><meshStandardMaterial color={colour[t.lineId] ?? "#fff"} emissive="#fff" emissiveIntensity={0.4} />
        </mesh>
      ))}
      <OrbitControls makeDefault />
    </Canvas>
  );
}
