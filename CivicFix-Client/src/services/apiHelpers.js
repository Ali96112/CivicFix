
export async function readBody(response) {
  const rawBody = await response.text();
  try {
    return rawBody ? JSON.parse(rawBody) : {};
  } catch {
    return { message: rawBody };
  }
}

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
export const STATUS_OPTIONS = ["Submitted", "In Progress", "Resolved", "Rejected"];
