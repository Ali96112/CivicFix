import { useState } from "react";
import MapPicker from "./MapPicker"; 
import { readBody, errorTextOf, uploadPhoto } from "../../services/apiHelpers";

function CreateReportForm({ role, categories, onCreated }) {
  const [formData, setFormData] = useState({
    Title: "",
    Description: "",
    CategoryId: "",
    ReportedPhotoUrl: "",
    ResolvedPhotoUrl: "", 
    Latitude: "",
    Longitude: "",
    Priority: "",
  });
  const [reportedPhotoFile, setReportedPhotoFile] = useState(null);
  const [resolvedPhotoFile, setResolvedPhotoFile] = useState(null);

  const [showMap, setShowMap] = useState(false);
  const canUseMap = role === "Admin" || role === "Staff";

  const [showCoords, setShowCoords] = useState(false);

  const [locationStatus, setLocationStatus] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const getLocation = () => {
    setLocationStatus("Getting your location...");
    if (!navigator.geolocation) {
      setLocationStatus("Your browser does not support location.");
      return;
    }
    
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setFormData({
          ...formData,
          Latitude: position.coords.latitude,
          Longitude: position.coords.longitude,
        });
        setLocationStatus("📍 Location captured successfully!");
      },
      () => {
        setLocationStatus("Could not get location. Please allow access.");
      },
    );
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");

    if (!formData.Latitude || !formData.Longitude) {
      setError("Please capture your location first.");
      return;
    }

    setLoading(true);
    try {
      const token = localStorage.getItem("token");
      const userId = localStorage.getItem("usr_Id");

      let reportedPhotoUrl = null;
      if (reportedPhotoFile) {
        reportedPhotoUrl = await uploadPhoto(reportedPhotoFile);
      }

      let resolvedPhotoUrl = null;
      if (resolvedPhotoFile) {
        resolvedPhotoUrl = await uploadPhoto(resolvedPhotoFile);
      }

      const response = await fetch("http://localhost:5140/api/Reports", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          ...formData,
          CategoryId: parseInt(formData.CategoryId),
          ReportedPhotoUrl: reportedPhotoUrl,
          ReporterId: parseInt(userId),
          Priority: formData.Priority || null,
          ResolvedPhotoUrl: resolvedPhotoUrl,
        }),
      });

      const data = await readBody(response);

      if (response.ok) {
        if (data.existingReportId) {
          setSuccess("This issue was already reported.");
        } else {
          setSuccess("✅ Report submitted successfully!");
          setFormData({
            Title: "", Description: "", CategoryId: "",
            ReportedPhotoUrl: "", ResolvedPhotoUrl: "",
            Latitude: "", Longitude: "", Priority: "",
          });
          setReportedPhotoFile(null);
          setResolvedPhotoFile(null);
          setLocationStatus("");
          setShowMap(false);
          onCreated(); 
        }
      } else {
        
        setError(errorTextOf(data, "Could not submit report."));
      }
    } catch (err) {
     
      setError(err.message || "Could not connect to server.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="report-form-dropdown">
      <form className="report-form" onSubmit={handleSubmit}>

        <div className="form-group">
          <label className="form-label">Category / الفئة</label>
          <select
            className="form-input"
            name="CategoryId"
            value={formData.CategoryId}
            onChange={handleChange}
            required
          >
            <option value="">Select a category...</option>
           
            {categories.map((cat) => (
              <option key={cat.ctg_Id} value={cat.ctg_Id}>
                {cat.ctg_Name}
              </option>
            ))}
          </select>
        </div>

        {role === "Resident" && (
          <div className="form-group">
            <label className="form-label">Priority / الأولوية</label>
            <select
              className="form-input"
              name="Priority"
              value={formData.Priority}
              onChange={handleChange}
            >
              <option value="">No priority</option>
              <option value="Low">🟢 Low</option>
              <option value="Medium">🟡 Medium</option>
              <option value="High">🔴 High</option>
            </select>
          </div>
        )}

        
        <div className="form-group">
          <label className="form-label">Photo of the problem / صورة المشكلة</label>
          <input
            className="form-input"
            type="file"
            accept="image/*"
            onChange={(e) => setReportedPhotoFile(e.target.files[0])}
          />
          {reportedPhotoFile && (
            <p className="location-status">📷 {reportedPhotoFile.name}</p>
          )}
        </div>

        <div className="form-group">
          <label className="form-label">Title / العنوان</label>
          <input
            className="form-input"
            type="text"
            name="Title"
            placeholder="e.g. Large pothole on main road"
            value={formData.Title}
            onChange={handleChange}
            required
          />
        </div>

        <div className="form-group">
          <label className="form-label">Description / الوصف</label>
          <textarea
            className="form-input form-textarea"
            name="Description"
            placeholder="Describe the issue in detail..."
            value={formData.Description}
            onChange={handleChange}
            rows="3"
            required
          />
        </div>

        
        {role === "Staff" && (
          <div className="form-group">
            <label className="form-label">Photo after fixing / صورة بعد الإصلاح</label>
            <input
              className="form-input"
              type="file"
              accept="image/*"
              onChange={(e) => setResolvedPhotoFile(e.target.files[0])}
            />
            {resolvedPhotoFile && (
              <p className="location-status">📷 {resolvedPhotoFile.name}</p>
            )}
          </div>
        )}

        <div className="form-group">
          <label className="form-label">Location / الموقع</label>

          <button type="button" className="btn-location" onClick={getLocation}>
            📍 Capture My Location
          </button>

         
          {canUseMap && (
            <button
              type="button"
              className="btn-location btn-location--map"
              onClick={() => setShowMap(!showMap)}
            >
              {showMap ? "✕ Close Map" : "🗺️ Pick on Map"}
            </button>
          )}

          {canUseMap && (
            <button
              type="button"
              className="btn-location btn-location--map"
              onClick={() => setShowCoords(!showCoords)}
            >
              {showCoords ? "✕ Close Coordinates" : "⌨️ Type Coordinates"}
            </button>
          )}

          {canUseMap && showCoords && (
            <div className="coord-entry">
              <div className="coord-entry__row">
                <label className="form-label">Latitude</label>
                
                <input
                  className="form-input"
                  type="number"
                  step="any"
                  placeholder="33.8751799450001"
                  value={formData.Latitude}
                  onChange={(e) =>
                    setFormData({ ...formData, Latitude: e.target.value })
                  }
                />
              </div>

              <div className="coord-entry__row">
                <label className="form-label">Longitude</label>
                <input
                  className="form-input"
                  type="number"
                  step="any"
                  placeholder="35.5197367960001"
                  value={formData.Longitude}
                  onChange={(e) =>
                    setFormData({ ...formData, Longitude: e.target.value })
                  }
                />
              </div>

            </div>
          )}

          {canUseMap && showMap && (
            <MapPicker
              initialLat={formData.Latitude}
              initialLng={formData.Longitude}
             
              onPick={(lat, lng) => {
                setFormData((current) => ({
                  ...current,
                  Latitude: lat,
                  Longitude: lng,
                }));
                setLocationStatus(
                  `🗺️ Location chosen on map: ${lat.toFixed(5)}, ${lng.toFixed(5)}`,
                );
              }}
            />
          )}

          {locationStatus && <p className="location-status">{locationStatus}</p>}
        </div>

        {error && <div className="form-error">{error}</div>}
        {success && <div className="form-success">{success}</div>}

        <button className="btn-submit" type="submit" disabled={loading}>
          {loading ? "Submitting..." : "Submit Report / إرسال البلاغ"}
        </button>

      </form>
    </div>
  );
}

export default CreateReportForm;
