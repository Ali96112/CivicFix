import { useState } from "react";
import MapPicker from "./MapPicker"; // click-a-spot map, for Admin and Staff
import { readBody, errorTextOf, uploadPhoto } from "../services/apiHelpers";

// The "🚨 Report a Problem" dropdown form.
//
// Split out of ReportForm.jsx — this was about a third of that file. It owns all
// of its own state, so the parent only has to render it and be told when a
// report was created.
//
// Props:
//   role       — decides which fields appear (priority for Resident, "after"
//                photo for Staff, map + coordinates for Admin/Staff)
//   categories — the dropdown options, already fetched by the parent
//   onCreated  — called after a successful submit so the parent reloads the list
function CreateReportForm({ role, categories, onCreated }) {
  const [formData, setFormData] = useState({
    Title: "",
    Description: "",
    CategoryId: "",
    ReportedPhotoUrl: "",
    ResolvedPhotoUrl: "", // staff only — the "after the fix" photo
    Latitude: "",
    Longitude: "",
    Priority: "",
  });

  // The real image FILES the user picked, held until submit.
  // These are browser File objects, not strings — they are uploaded to
  // POST api/Uploads inside handleSubmit, and the URLs it returns are what
  // actually get sent to the API as ReportedPhotoUrl / ResolvedPhotoUrl.
  const [reportedPhotoFile, setReportedPhotoFile] = useState(null);
  const [resolvedPhotoFile, setResolvedPhotoFile] = useState(null);

  // Is the map open? Only Admin and Staff ever see the toggle for it.
  // A resident standing at the pothole should use real GPS, not pick a spot
  // from a map, so residents keep the GPS button only.
  const [showMap, setShowMap] = useState(false);
  const canUseMap = role === "Admin" || role === "Staff";

  // The "type the coordinates" panel, also Admin and Staff only.
  //
  // WHY IT EXISTS: GPS gives you wherever you are standing, and the map is a
  // blind click because baladiye boundaries are not drawn on it. Neither lets
  // you file a report at an EXACT point — which you need to test the shared
  // report case, where a point on a border is assigned to two baladiyat at once.
  const [showCoords, setShowCoords] = useState(false);
  const [coordCheck, setCoordCheck] = useState("");
  const [checkingCoords, setCheckingCoords] = useState(false);

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

  // Asks the API which baladiyat the typed coordinates fall into, BEFORE the
  // report is submitted. Calls GET api/Reports/location-check.
  //
  // This is what makes the shared-report case testable: paste a border point,
  // press check, and you can see whether it matches one baladiye or two before
  // you commit to filing anything.
  const checkCoordinates = async () => {
    if (!formData.Latitude || !formData.Longitude) {
      setCoordCheck("Enter both a latitude and a longitude first.");
      return;
    }

    setCheckingCoords(true);
    setCoordCheck("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/location-check?latitude=${formData.Latitude}&longitude=${formData.Longitude}`,
        { headers: { Authorization: `Bearer ${token}` } },
      );

      const body = await readBody(response);

      if (!response.ok) {
        setCoordCheck(errorTextOf(body, "Could not check this location."));
        return;
      }

      const matches = body.MatchingBaladiyat || [];

      if (matches.length === 0) {
        setCoordCheck(
          "⚠️ This point is not inside any baladiye — a report here would be rejected.",
        );
      } else if (matches.length === 1) {
        setCoordCheck(
          `✅ One baladiye: ${matches[0].mun_Name}. This report will not be shared.`,
        );
      } else {
        // this is the case worth demonstrating — two or more baladiyat means the
        // report lands on the Admin's Shared Reports tab for a decision
        const names = matches.map((m) => m.mun_Name).join(" + ");
        setCoordCheck(
          `🔀 ${matches.length} baladiyat: ${names}. This report will be SHARED and will need an admin to choose who handles it.`,
        );
      }
    } catch (err) {
      setCoordCheck("Could not connect to server.");
    } finally {
      setCheckingCoords(false);
    }
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

      // Upload the chosen image files FIRST, and keep the URLs the API gives
      // back. This has to happen before the report is created, because the
      // report row stores the photo URL, so the file must already be on the
      // server by then. If an upload fails it throws, and the catch below shows
      // the reason — the report is not created with a broken photo link.
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
          // These two are not typed by the user. They are the URLs returned by
          // POST api/Uploads above, after the real files were saved into the
          // API's wwwroot/uploads folder.
          ReportedPhotoUrl: reportedPhotoUrl,
          // NOTE: the backend IGNORES this and takes the reporter's Id from the
          // JWT token instead, because a body field can be faked. It is still
          // sent so nothing breaks, but it no longer decides who owns the report.
          ReporterId: parseInt(userId),
          // Priority is a STRING in the database ("Low" / "Medium" / "High").
          // It used to be parseInt(...) here, which turned "High" into NaN and
          // then null, so the resident's priority was silently thrown away.
          Priority: formData.Priority || null,
          // only staff send an "after" photo; for anyone else this stays null,
          // so an empty string is never stored as a photo URL
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
          onCreated(); // tell the parent to reload the list and close this form
        }
      } else {
        // the API returns a bare string for most errors, so the real reason
        // ("You can only submit reports within your baladiye boundaries.")
        // actually reaches the user
        setError(errorTextOf(data, "Could not submit report."));
      }
    } catch (err) {
      // uploadPhoto throws a real Error with the server's reason
      // ("The photo is too large.", "Only image files are allowed.")
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
            {/* the backend (CategoriesController) returns rows shaped like
                { ctg_Id: 1, ctg_Name: "Roads" } */}
            {categories.map((cat) => (
              <option key={cat.ctg_Id} value={cat.ctg_Id}>
                {cat.ctg_Name}
              </option>
            ))}
          </select>
        </div>

        {/*
          Priority picker, shown ONLY to Residents.
          This is the "resident can set priority" rule: the resident chooses the
          starting priority, and other residents can change it later by voting
          (POST api/Reports/{id}/priority). Staff and Admin do not see this —
          the backend nulls their Priority anyway, so showing it would be a lie.
        */}
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
              {/* these three strings must match the backend exactly —
                  ReportsController rejects anything else */}
              <option value="Low">🟢 Low</option>
              <option value="Medium">🟡 Medium</option>
              <option value="High">🔴 High</option>
            </select>
          </div>
        )}

        {/*
          "photo after the fix", shown ONLY to Staff.
          The backend inserts a Staff report with status 'Resolved' straight away
          and saves request.ResolvedPhotoUrl.
        */}
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

        {/*
          A real file picker: you choose an image from your phone or computer,
          and on submit the file is uploaded to POST api/Uploads, which saves it
          in the API's wwwroot/uploads folder and returns a URL. That returned URL
          is what gets stored in rpt_ReportedPhotoUrl — so the database column did
          not change, only how the URL is produced.
        */}
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
          <label className="form-label">Location / الموقع</label>

          <button type="button" className="btn-location" onClick={getLocation}>
            📍 Capture My Location
          </button>

          {/*
            "Pick on Map", for Admin and Staff only.
            A resident is standing at the problem, so GPS is right for them. An
            admin or staff member is usually at a desk reporting something they
            were told about, and GPS would give the office location.

            The baladiye is still worked out automatically by the backend:
            CreateReport runs a spatial query on whatever lat/long it receives.
          */}
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
                {/* step="any" matters: without it a number input rejects
                    decimals, and every coordinate is a decimal */}
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

              <button
                type="button"
                className="btn-location"
                disabled={checkingCoords}
                onClick={checkCoordinates}
              >
                {checkingCoords ? "Checking..." : "🔎 Check this location"}
              </button>

              {coordCheck && <p className="coord-entry__result">{coordCheck}</p>}
            </div>
          )}

          {canUseMap && showMap && (
            <MapPicker
              initialLat={formData.Latitude}
              initialLng={formData.Longitude}
              // called every time the admin clicks a new spot on the map
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
