// NEW FILE — the map that Admin and Staff use to choose a report's location
// by clicking, instead of relying on the browser's GPS.
//
// WHY A SEPARATE FILE: ReportForm.jsx is already long, and a map has its own
// lifecycle (create it, listen for clicks, destroy it). Keeping it here means
// ReportForm just renders <MapPicker onPick={...} /> and gets lat/long back.
//
// REQUIRES ONE PACKAGE — run this once in the CivicFix-Client folder:
//     npm install leaflet
// Leaflet is free, uses OpenStreetMap tiles, and needs no API key or account.

import { useEffect, useRef, useState } from "react";
import L from "leaflet";          // the map library itself
import "leaflet/dist/leaflet.css"; // its stylesheet — without this the map renders broken

// Fallback centre: roughly the middle of Lebanon, used when the browser
// will not give us the current location (permission denied, no GPS, etc.)
const LEBANON_CENTER = [33.8547, 35.8623];
const LEBANON_ZOOM = 9;   // zoomed out enough to see the whole country
const LOCATED_ZOOM = 15;  // zoomed in close once we know where the user is

function MapPicker({ onPick, initialLat, initialLng }) {
  // useRef holds a value that survives re-renders WITHOUT causing one.
  // mapDivRef  → the actual <div> the map is drawn into
  // mapRef     → the Leaflet map object, so we can clean it up later
  // markerRef  → the pin, so each new click moves it instead of adding another
  const mapDivRef = useRef(null);
  const mapRef = useRef(null);
  const markerRef = useRef(null);

  const [status, setStatus] = useState("Finding your location...");

  useEffect(() => {
    // guard: if this effect somehow runs twice (React 18+ StrictMode does this
    // in development), don't build a second map on the same div
    if (mapRef.current) {
      return;
    }

    // ── 1. create the map ──
    const map = L.map(mapDivRef.current).setView(LEBANON_CENTER, LEBANON_ZOOM);
    mapRef.current = map;

    // the actual map images. OpenStreetMap is free but the attribution
    // line is required by their terms — do not remove it.
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);

    // ── 2. a helper that drops (or moves) the pin and reports the coordinates up ──
    const placePin = (lat, lng) => {
      // Leaflet's default pin image breaks when bundled by Vite, because the
      // library looks for icon files at a path that does not exist after the
      // build. A divIcon is just HTML, so it sidesteps that problem entirely.
      const pinIcon = L.divIcon({
        className: "", // no default leaflet classes, we style it inline below
        html: '<div style="font-size:28px;line-height:28px;transform:translate(-50%,-100%)">📍</div>',
      });

      if (markerRef.current) {
        markerRef.current.setLatLng([lat, lng]); // move the existing pin
      } else {
        markerRef.current = L.marker([lat, lng], { icon: pinIcon }).addTo(map);
      }

      setStatus(`📍 Selected: ${lat.toFixed(5)}, ${lng.toFixed(5)}`);

      // hand the coordinates back to ReportForm so it can put them in formData
      onPick(lat, lng);
    };

    // ── 3. clicking anywhere on the map chooses that spot ──
    map.on("click", (event) => {
      placePin(event.latlng.lat, event.latlng.lng);
    });

    // ── 4. if the form already had coordinates, show them ──
    if (initialLat && initialLng) {
      map.setView([initialLat, initialLng], LOCATED_ZOOM);
      placePin(Number(initialLat), Number(initialLng));
    } else if (navigator.geolocation) {
      // otherwise centre the map on where the user is right now, so an admin
      // in the office starts looking at their own area instead of the whole country.
      // NOTE: this only MOVES the map — it does not choose a location.
      // The admin still has to click to drop the pin.
      navigator.geolocation.getCurrentPosition(
        (position) => {
          // the map may already be gone if the form was closed quickly
          if (!mapRef.current) {
            return;
          }
          map.setView([position.coords.latitude, position.coords.longitude], LOCATED_ZOOM);
          setStatus("Click on the map to choose the exact spot.");
        },
        () => {
          // permission denied or GPS unavailable — stay on the Lebanon view
          setStatus("Could not find your location. Click on the map to choose a spot.");
        },
      );
    } else {
      setStatus("Click on the map to choose a spot.");
    }

    // ── 5. cleanup ──
    // React runs this when the component is removed (the admin closes the form).
    // Without it, Leaflet leaves listeners and a hidden map behind, and reopening
    // the form throws "Map container is already initialized".
    return () => {
      map.remove();
      mapRef.current = null;
      markerRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // [] = build the map once, when the picker first appears

  return (
    <div className="map-picker">
      {/* Leaflet draws into this div. It MUST have a height in CSS or the
          map is invisible — see .map-picker__canvas in Report.css */}
      <div ref={mapDivRef} className="map-picker__canvas" />
      <p className="map-picker__status">{status}</p>
    </div>
  );
}

export default MapPicker;
