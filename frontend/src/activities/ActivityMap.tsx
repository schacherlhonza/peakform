import { useMemo } from 'react';
import { CircleMarker, MapContainer, Polyline, TileLayer, Tooltip } from 'react-leaflet';
import type { LatLngBoundsExpression, LatLngTuple } from 'leaflet';
import 'leaflet/dist/leaflet.css';
import classes from './ActivityMap.module.css';

// OpenStreetMap's own tile servers — fine for development and low traffic, but their usage policy
// requires a different provider at scale (see docs/integrations/strava-archive-import.md). They
// also reject tile requests without a Referer, while nginx sends `Referrer-Policy: no-referrer`
// globally — hence the per-layer override below.
const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

export interface ActivityMapProps {
  latitude: readonly (number | null | undefined)[];
  longitude: readonly (number | null | undefined)[];
  times: readonly number[];
  /** Time offset hovered in one of the charts — shown as a marker on the route. */
  hoverTime: number | null;
  startLabel: string;
  finishLabel: string;
}

/** Index of the sample closest to `t` (times are ascending). */
function indexAt(times: readonly number[], t: number): number {
  let lo = 0;
  let hi = times.length - 1;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    if (times[mid] < t) lo = mid + 1;
    else hi = mid;
  }
  return lo > 0 && Math.abs(times[lo - 1] - t) < Math.abs(times[lo] - t) ? lo - 1 : lo;
}

/** Route of one activity. Loaded lazily from ActivityDetailPage so Leaflet stays out of the main bundle. */
export default function ActivityMap({ latitude, longitude, times, hoverTime, startLabel, finishLabel }: ActivityMapProps) {
  const route = useMemo(() => {
    const points: LatLngTuple[] = [];
    const sampleIndex: number[] = [];
    const len = Math.min(latitude.length, longitude.length);
    for (let i = 0; i < len; i++) {
      const lat = latitude[i];
      const lon = longitude[i];
      if (lat == null || lon == null) continue;
      points.push([lat, lon]);
      sampleIndex.push(i);
    }
    return { points, sampleIndex };
  }, [latitude, longitude]);

  const bounds = useMemo<LatLngBoundsExpression | null>(() => {
    if (route.points.length === 0) return null;
    const lats = route.points.map((p) => p[0]);
    const lons = route.points.map((p) => p[1]);
    return [
      [Math.min(...lats), Math.min(...lons)],
      [Math.max(...lats), Math.max(...lons)],
    ];
  }, [route]);

  // Nearest point with a GPS fix to the hovered time.
  const hoverPoint = useMemo(() => {
    if (hoverTime == null || route.points.length === 0) return null;
    const sample = indexAt(times, hoverTime);
    const k = indexAt(route.sampleIndex, sample);
    return route.points[k];
  }, [hoverTime, times, route]);

  if (!bounds) return null;
  const start = route.points[0];
  const finish = route.points[route.points.length - 1];

  return (
    <div className={classes.map}>
      <MapContainer bounds={bounds} boundsOptions={{ padding: [24, 24] }} scrollWheelZoom={false} style={{ height: '100%', width: '100%' }}>
        <TileLayer url={TILE_URL} attribution={ATTRIBUTION} maxZoom={19} referrerPolicy="strict-origin-when-cross-origin" />
        <Polyline positions={route.points} pathOptions={{ color: '#c7f34d', weight: 4, opacity: 0.9 }} />
        <CircleMarker center={start} radius={6} pathOptions={{ color: '#0b120d', weight: 2, fillColor: '#51dfb0', fillOpacity: 1 }}>
          <Tooltip>{startLabel}</Tooltip>
        </CircleMarker>
        <CircleMarker center={finish} radius={6} pathOptions={{ color: '#0b120d', weight: 2, fillColor: '#ff7e72', fillOpacity: 1 }}>
          <Tooltip>{finishLabel}</Tooltip>
        </CircleMarker>
        {hoverPoint && (
          <CircleMarker center={hoverPoint} radius={7} pathOptions={{ color: '#ffffff', weight: 2, fillColor: '#c7f34d', fillOpacity: 1 }} />
        )}
      </MapContainer>
    </div>
  );
}
