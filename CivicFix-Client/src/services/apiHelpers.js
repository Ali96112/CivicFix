
export async function readBody(response) {//readBody() takes the content that came back from the backend and converts it into a JavaScript object that your frontend can easily use.
  const rawBody = await response.text();
  try {
    return rawBody ? JSON.parse(rawBody) : {};//changes that JSON text into a JavaScript object
  } catch {
    return { message: rawBody };
  }
}

export function errorTextOf(data, fallback) {//Look at the error data from the backend and pick the best error message to show.
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

export async function uploadPhoto(file) {//Take a photo selected in React, send it to the backend, and return the URL of the saved photo
  const token = localStorage.getItem("token");

  const formDataToSend = new FormData();
  formDataToSend.append("file", file); // "file" must match `IFormFile file` in UploadsController//Put the photo inside FormData

  const response = await fetch("http://localhost:5140/api/Uploads", {//Wait for backend response
    method: "POST",
    headers: { Authorization: `Bearer ${token}` }, // no Content-Type on purpose
    body: formDataToSend,
  });

  const body = await readBody(response);//Read the backend response

  if (!response.ok) {
    // thrown so the caller's catch shows the reason ("too large", "only images", ...)
    throw new Error(errorTextOf(body, "Photo upload failed."));
  }

  return body.url;//it return url of photo
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
