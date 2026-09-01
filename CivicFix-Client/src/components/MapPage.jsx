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
  return "#e0a23c"; // amber — In Progress
}

function MapPage() {
  const navigate = useNavigate();
  const [reports, setReports] = useState([]);

  const mapDivRef = useRef(null);   // A reference to the actual HTML <div> where Leaflet will draw the map.
  const mapRef = useRef(null);      // A reference that stores the actual Leaflet map object after the map is created./map it self
  const markersRef = useRef(null);  //A reference that stores one Leaflet layer group containing all the report markers/pins.

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

    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {//for streets roads
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);

    
    markersRef.current = L.layerGroup().addTo(map);//Create one Leaflet group for markers

    return () => {//when closed the page
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

   
      const pinIcon = L.divIcon({//This creates a custom Leaflet marker icon for one report.
        className: "",
        html:
          `<div style="width:16px;height:16px;border-radius:50%;` +
          `border:2px solid #fff;box-shadow:0 0 4px rgba(0,0,0,.5);` +
          `background:${colorForStatus(report.rpt_Status)}"></div>`,
      });

      const marker = L.marker([report.Latitude, report.Longitude], {//draw it on map
        icon: pinIcon,
      });

      // This code is building the popup content that will appear when you click a report marker.
      const popupNode = document.createElement("div");//Create a real HTML <div> using JavaScript.
      popupNode.className = "map-popup";

      const titleEl = document.createElement("strong");//Create a <strong> element for the report title.
      
      titleEl.textContent = report.rpt_Title; //report title
      titleEl.textContent = `#${report.rpt_Id} — ${report.rpt_Title}`;
      const statusEl = document.createElement("p");
      statusEl.textContent = `${report.rpt_Status} · ${report.CategoryName}`;

      const muniEl = document.createElement("p");
      muniEl.textContent = `🏛️ ${report.AssignedMunicipalities}`;

      popupNode.appendChild(titleEl);//puting them inside popup
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

      marker.bindPopup(popupNode);//connect popup to marker
      markersRef.current.addLayer(marker);//Add this marker into the marker group we created earlie
      points.push([report.Latitude, report.Longitude]);//Save this report's coordinates into the points array.
    });

    // zoom so every pin is visible instead of always showing all of Lebanon
    if (points.length > 0) {
      mapRef.current.fitBounds(points, { padding: [50, 50], maxZoom: 15 });
    }
  }, [reports, navigate]);//whennever the report changes refetch the pins

  return (
    <div className="map-page">
      <Navbar />
      <p className="map-page__count">{reports.length} reports loaded</p>
      <div ref={mapDivRef} className="map-page__canvas" />
    </div>
  );
}

export default MapPage;