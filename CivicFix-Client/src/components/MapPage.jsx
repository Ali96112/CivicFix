import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import Navbar from "./layout/Navbar";
import "../styles/MapPage.css";

const LEBANON_CENTER = [33.8547, 35.8623];
const LEBANON_ZOOM = 9;

// One colour per status. A helper function rather than a ternary chain
// buried inside the marker code — easier to read and to change later.
function colorForStatus(status) {
  if (status === "Resolved") {
    return "#4aa85a"; // green
  }
  if (status === "Submitted") {
    return "#3d8fd4"; // blue
  }
  if (status === "Rejected") {
    return "#8a8a8a"; // grey
  }
  return "#e0a23c"; // amber — In Progress
}

function MapPage() {
  const navigate = useNavigate();
  const [reports, setReports] = useState([]);

  const mapDivRef = useRef(null);   // the <div> Leaflet draws into
  const mapRef = useRef(null);      // the Leaflet map object
  const markersRef = useRef(null);  // ONE layer group holding every pin

  // ── effect 1: fetch the reports, once ──
  useEffect(() => {
    const fetchReports = async () => {
      try {
        const response = await fetch("http://localhost:5140/api/Reports/map");
        if (response.ok) {
          const data = await response.json();
          setReports(data);
        }
      } catch (err) {
        // the map still renders, just with no pins
      }
    };

    fetchReports();
  }, []);

  // ── effect 2: build the map, once ──
  useEffect(() => {
    if (mapRef.current) {
      return;
    }

    const map = L.map(mapDivRef.current).setView(LEBANON_CENTER, LEBANON_ZOOM);
    mapRef.current = map;

    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);

    // added to the map once, then emptied and refilled forever after
    markersRef.current = L.layerGroup().addTo(map);

    return () => {
      map.remove();
      mapRef.current = null;
      markersRef.current = null;
    };
  }, []);

  // ── effect 3: draw the pins whenever the reports change ──
  useEffect(() => {
    if (!markersRef.current) {
      return; // map not built yet
    }

    markersRef.current.clearLayers(); // wipe old pins before drawing new ones

    const points = [];                            // collected to zoom-to-fit at the end
    const token = localStorage.getItem("token");  // is anyone logged in?

    reports.forEach((report) => {
      if (report.Latitude == null || report.Longitude == null) {
        return; // nothing to place
      }

      // A divIcon is plain HTML — the same trick MapPicker uses to dodge
      // Leaflet's broken default icon path under Vite.
      const pinIcon = L.divIcon({
        className: "",
        html:
          `<div style="width:16px;height:16px;border-radius:50%;` +
          `border:2px solid #fff;box-shadow:0 0 4px rgba(0,0,0,.5);` +
          `background:${colorForStatus(report.rpt_Status)}"></div>`,
      });

      const marker = L.marker([report.Latitude, report.Longitude], {
        icon: pinIcon,
      });

      // Build the popup as real DOM, not an HTML string. That lets us attach a
      // normal click listener and use navigate() instead of a plain <a>,
      // which would reload the whole React app.
      const popupNode = document.createElement("div");
      popupNode.className = "map-popup";

      const titleEl = document.createElement("strong");
      titleEl.textContent = report.rpt_Title; // textContent, so a title with
                                              // < or > can't break the popup

      const statusEl = document.createElement("p");
      statusEl.textContent = `${report.rpt_Status} · ${report.CategoryName}`;

      const muniEl = document.createElement("p");
      muniEl.textContent = `🏛️ ${report.AssignedMunicipalities}`;

      popupNode.appendChild(titleEl);
      popupNode.appendChild(statusEl);
      popupNode.appendChild(muniEl);

      // The detail page needs a login, so only offer the link to someone
      // who actually has a token.
      if (token) {
        const openButton = document.createElement("button");
        openButton.textContent = "Open report →";
        openButton.addEventListener("click", () => {
          navigate(`/report/${report.rpt_Id}`);
        });
        popupNode.appendChild(openButton);
      }

      marker.bindPopup(popupNode);
      markersRef.current.addLayer(marker);
      points.push([report.Latitude, report.Longitude]);
    });

    // zoom so every pin is visible instead of always showing all of Lebanon
    if (points.length > 0) {
      mapRef.current.fitBounds(points, { padding: [50, 50], maxZoom: 15 });
    }
  }, [reports, navigate]);

  return (
    <div className="map-page">
      <Navbar />
      <p className="map-page__count">{reports.length} reports loaded</p>
      <div ref={mapDivRef} className="map-page__canvas" />
    </div>
  );
}

export default MapPage;