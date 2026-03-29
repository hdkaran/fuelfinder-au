import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { importLibrary, setOptions } from '@googlemaps/js-api-loader';
import type { StationDto, StationStatus } from '../types';
import styles from './StationMap.module.css';

const STATUS_COLORS: Record<StationStatus, string> = {
  available: '#22c55e',
  low:       '#f97316',
  out:       '#ef4444',
  unknown:   '#9ca3af',
};

// Configure the Maps JS API key once at module load time
setOptions({ key: import.meta.env.VITE_GOOGLE_MAPS_API_KEY ?? '', v: 'weekly' });

interface Props {
  stations: StationDto[];
  center: { lat: number; lng: number };
}

function markerIcon(status: StationStatus): google.maps.Symbol {
  return {
    path: google.maps.SymbolPath.CIRCLE,
    fillColor: STATUS_COLORS[status],
    fillOpacity: 1,
    strokeColor: '#fff',
    strokeWeight: 2,
    scale: 8,
  };
}

export default function StationMap({ stations, center }: Props) {
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstance = useRef<google.maps.Map | null>(null);
  const markers = useRef<google.maps.Marker[]>([]);
  const stationsRef = useRef(stations);
  const navigate = useNavigate();

  // Keep stationsRef current so the async init callback sees the latest data
  stationsRef.current = stations;

  // Initialise map once — then render current stations immediately after load
  useEffect(() => {
    if (!mapRef.current) return;

    importLibrary('maps').then(({ Map }) => {
      if (mapInstance.current || !mapRef.current) return;

      const map = new Map(mapRef.current, {
        center,
        zoom: 14,
        disableDefaultUI: true,
        zoomControl: true,
        clickableIcons: false,
      });
      mapInstance.current = map;

      markers.current = stationsRef.current.map((s) => addMarker(map, s, navigate));
    });
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Refresh markers whenever stations data updates
  useEffect(() => {
    if (!mapInstance.current) return;
    markers.current.forEach((m) => m.setMap(null));
    markers.current = stations.map((s) => addMarker(mapInstance.current!, s, navigate));
  }, [stations, navigate]);

  if (!import.meta.env.VITE_GOOGLE_MAPS_API_KEY) {
    return (
      <div className={styles.placeholder}>
        <p>Map requires <code>VITE_GOOGLE_MAPS_API_KEY</code> to be set.</p>
      </div>
    );
  }

  return <div ref={mapRef} className={styles.map} />;
}

function addMarker(
  map: google.maps.Map,
  station: StationDto,
  navigate: (path: string) => void,
): google.maps.Marker {
  const marker = new google.maps.Marker({
    map,
    position: { lat: station.latitude, lng: station.longitude },
    title: station.name,
    icon: markerIcon(station.status),
  });
  marker.addListener('click', () => navigate(`/stations/${station.id}`));
  return marker;
}
