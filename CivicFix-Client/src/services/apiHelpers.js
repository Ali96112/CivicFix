// Shared helpers for talking to the CivicFix API.
//
// These were duplicated inside ReportForm.jsx and ReportDetail.jsx. Splitting
// ReportForm into components would have made that three copies, so they moved
// here. They are not UI, which is why they live in services/ and not components/.

// The API replies with JSON on success but with a PLAIN SENTENCE on most errors,
// so calling response.json() straight away throws on exactly the cases where you
// most need the message. This reads the text first and only parses when it can.
export async function readBody(response) {
  const rawBody = await response.text();
  try {
    return rawBody ? JSON.parse(rawBody) : {};
  } catch {
    return { message: rawBody };
  }
}

// Pull a readable sentence out of whatever the server sent back.
// Handles all three shapes the API can produce:
//   "a plain sentence"                    → BadRequest("...")
//   { message: "..." }                    → Ok(new { message = ... })
//   { title: "...", errors: {...} }       → ASP.NET model-binding failure
export function errorTextOf(data, fallback) {
  if (typeof data === "string") {
    return data;
  }
  if (data.message) {
    return data.message;
  }
  if (data.title) {
    return data.title;
  }
  return fallback;
}

// Sends ONE real image file to POST api/Uploads and returns the URL the API
// saved it at (e.g. http://localhost:5140/uploads/<guid>.jpg).
//
// Two things about this request are easy to get wrong:
//  1. the body is a FormData object, not JSON — that is how a browser sends
//     an actual file
//  2. we must NOT set a Content-Type header. The browser sets it itself,
//     because it has to append a random "boundary" marker that separates the
//     parts of the upload. Setting it by hand breaks the upload.
export async function uploadPhoto(file) {
  const token = localStorage.getItem("token");

  const formDataToSend = new FormData();
  formDataToSend.append("file", file); // "file" must match `IFormFile file` in UploadsController

  const response = await fetch("http://localhost:5140/api/Uploads", {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` }, // no Content-Type on purpose
    body: formDataToSend,
  });

  const body = await readBody(response);

  if (!response.ok) {
    // thrown so the caller's catch shows the reason ("too large", "only images", ...)
    throw new Error(errorTextOf(body, "Photo upload failed."));
  }

  return body.url;
}

// colored badge class based on status — used by the card and the detail page
export function getStatusClass(status) {
  if (status === "Resolved") return "report-badge--resolved";
  if (status === "Submitted") return "report-badge--submitted";
  return "report-badge--progress";
}

// the statuses the backend accepts — this list must match `allowedStatuses`
// in ReportsAdminController.UpdateReportStatus, or the API will reject the change
export const STATUS_OPTIONS = ["Submitted", "In Progress", "Resolved"];
