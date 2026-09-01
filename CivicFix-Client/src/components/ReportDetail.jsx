// NEW FILE — the full page for ONE report, at the route /report/:id
//
// Clicking any report card in the list comes here. It shows everything the
// backend knows about that report in a single request:
//   the two photos (before / after), title, description, category,
//   who reported it, the exact coordinates, which baladiyat it was assigned to,
//   the priority vote breakdown, the whole status trail, and the comments.
//
// Admin and Staff also get the change-status panel here.
// The backend already stops Staff from opening a report that belongs to a
// different baladiye (GetReportById returns 403), so this page does not need
// to police that itself — it just shows whatever error the API sends back.

import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import "../styles/Report.css";

// must match `allowedStatuses` in ReportsController.UpdateReportStatus
const STATUS_OPTIONS = ["Submitted", "In Progress", "Resolved", "Rejected"];

function ReportDetail() {
  // useParams reads the ":id" part out of the URL /report/7 → id = "7"
  const { id } = useParams();
  const navigate = useNavigate();

  const role = localStorage.getItem("usr_Role");
  const canEditStatus = role === "Admin" || role === "Staff";

  const [data, setData] = useState(null); // the whole API response
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // ── change-status panel state ──
  const [newStatus, setNewStatus] = useState("");
  const [resolvedPhotoFile, setResolvedPhotoFile] = useState(null);
  const [saving, setSaving] = useState(false);

  // ── new comment state ──
  const [commentText, setCommentText] = useState("");
  const [postingComment, setPostingComment] = useState(false);

  // ADDED — true while a priority vote or an agreement is being sent, so the
  // buttons can be disabled and cannot be double-clicked into a 400.
  const [voting, setVoting] = useState(false);

  // ── ADDED: "move this report to another baladiye", Admin only ──
  // municipalities  → every baladiye in the country, for the dropdown
  // moveSearch      → what the admin typed, to narrow that long list down
  // moveTargetId    → the baladiye they picked
  // moving          → true while the move request is running
  const [municipalities, setMunicipalities] = useState([]);
  const [moveSearch, setMoveSearch] = useState("");
  const [moveTargetId, setMoveTargetId] = useState("");
  const [moving, setMoving] = useState(false);

  // shared helper — the API answers with JSON on success but a plain sentence
  // on most errors, so response.json() alone throws exactly when it matters
  const readBody = async (response) => {
    const rawBody = await response.text();
    try {
      return rawBody ? JSON.parse(rawBody) : {};
    } catch {
      return { message: rawBody };
    }
  };

  const errorTextOf = (body, fallback) => {
    if (typeof body === "string") {
      return body;
    }
    if (body.message) {
      return body.message;
    }
    if (body.title) {
      return body.title;
    }
    return fallback;
  };

  const fetchReport = async () => {
    setLoading(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}`, {
        headers: { Authorization: `Bearer ${token}` },
      });

      const body = await readBody(response);

      if (response.ok) {
        setData(body);
        setNewStatus(body.Report.rpt_Status); // start the dropdown on the current status
      } else {
        setError(errorTextOf(body, "Could not load this report."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setLoading(false);
    }
  };

  // ADDED — loads every baladiye, for the Admin's "move to another baladiye"
  // dropdown. Only fetched for an Admin, since nobody else sees that panel.
  // GET api/Municipalities is the same public endpoint the leaderboard uses.
  const fetchMunicipalities = async () => {
    try {
      const response = await fetch("http://localhost:5140/api/Municipalities");
      if (response.ok) {
        const list = await response.json();
        setMunicipalities(list);
      }
    } catch (err) {
      // ignore — without this list the move panel just has nothing to pick from
    }
  };

  useEffect(() => {
    fetchReport();
    if (role === "Admin") {
      fetchMunicipalities(); // ADDED: only an Admin can move a report
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]); // re-run if the user navigates from one report straight to another

  // uploads one real file and gives back the URL the API saved it at.
  // Shared with ReportForm's version — see the comments there for why the
  // photo is a file now instead of a pasted link.
  const uploadPhoto = async (file) => {
    const token = localStorage.getItem("token");

    // FormData is how a browser sends a real file. Note we do NOT set a
    // Content-Type header — the browser must set it itself, because it has to
    // include the multipart boundary marker.
    const formData = new FormData();
    formData.append("file", file); // the name "file" must match IFormFile file

    const response = await fetch("http://localhost:5140/api/Uploads", {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
      body: formData,
    });

    const body = await readBody(response);

    if (!response.ok) {
      throw new Error(errorTextOf(body, "Photo upload failed."));
    }

    return body.url;
  };

  const saveStatus = async () => {
    // the backend refuses "Resolved" without a photo of the fix
    if (newStatus === "Resolved" && !resolvedPhotoFile && !data.Report.rpt_ResolvedPhotoUrl) {
      setError("Choose a photo of the fix before marking this report Resolved.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      // if a new file was chosen, upload it first and use the URL it returns.
      // otherwise keep whatever photo the report already had.
      let photoUrl = data.Report.rpt_ResolvedPhotoUrl;
      if (resolvedPhotoFile) {
        photoUrl = await uploadPhoto(resolvedPhotoFile);
      }

      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}/status`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          NewStatus: newStatus,
          ResolvedPhotoUrl: photoUrl || null,
          // ignored by the backend now (it trusts the JWT instead), sent only
          // so the request shape stays the same
          ChangedByUserId: parseInt(localStorage.getItem("usr_Id")),
        }),
      });

      const body = await readBody(response);

      if (response.ok) {
        setResolvedPhotoFile(null);
        fetchReport(); // reload so the new status and history row appear
      } else {
        setError(errorTextOf(body, "Could not update the status."));
      }
    } catch (err) {
      setError(err.message || "Could not connect to server.");
    } finally {
      setSaving(false);
    }
  };

  // ADDED — a Resident votes on how urgent a report is.
  // Calls POST api/Reports/{id}/priority. The backend counts every resident's
  // vote and sets the report's priority to whichever value has the most, so one
  // person cannot decide it alone. It also refuses a second vote from the same
  // person, and only allows voting on RESIDENT-submitted reports.
  const votePriority = async (priority) => {
    setVoting(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}/priority`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          Priority: priority, // must be exactly "Low", "Medium" or "High"
          UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, the JWT decides
        }),
      });

      const body = await readBody(response);

      if (response.ok) {
        fetchReport(); // reload so the new tally and the winning priority show
      } else {
        setError(errorTextOf(body, "Could not submit your priority vote."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setVoting(false);
    }
  };

  // ADDED — a Resident confirms (or disputes) that a baladiye really did the work.
  //
  // This is the accountability loop: a Staff member submits a report of work they
  // have finished, and residents who can see the place vouch for it. The backend
  // only awards the baladiye its +10 points once THREE residents have agreed, so
  // a baladiye cannot award itself points by filing reports about work it did not
  // actually do. Calls POST api/Reports/{id}/agree.
  const submitAgreement = async (isAgreement) => {
    setVoting(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}/agree`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          IsAgreement: isAgreement, // true = "yes they did it", false = "no they did not"
          UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, the JWT decides
        }),
      });

      const body = await readBody(response);

      if (response.ok) {
        fetchReport(); // reload so the 👍/👎 counts update
      } else {
        setError(errorTextOf(body, "Could not submit your answer."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setVoting(false);
    }
  };

  // ADDED — the Admin moves this report to a different baladiye.
  //
  // This is NOT the same as the "choose a handler" buttons on the Shared Reports
  // tab. That one picks among the baladiyat the spatial query already found;
  // this one can hand the report to ANY baladiye in Lebanon, which is what you
  // need when the automatic assignment got it wrong outright.
  //
  // The backend replaces the report's assignments and, if the report was already
  // resolved, moves the +10 from the old baladiye to the new one.
  const moveReport = async () => {
    if (!moveTargetId) {
      setError("Choose a baladiye to move this report to.");
      return;
    }

    // find the name so the confirmation dialog can say where it is going
    const target = municipalities.find(
      (m) => String(m.mun_Id) === String(moveTargetId),
    );

    const confirmed = window.confirm(
      `Move this report to ${target ? target.mun_Name : "the selected baladiye"}?\n\n` +
        "It will be removed from its current baladiye. If the report was already " +
        "resolved, the points move across too.",
    );
    if (!confirmed) {
      return;
    }

    setMoving(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}/move`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ MunicipalityId: parseInt(moveTargetId) }),
      });

      const body = await readBody(response);

      if (response.ok) {
        setMoveSearch("");
        setMoveTargetId("");
        fetchReport(); // reload so the new baladiye shows in the assignments list
      } else {
        setError(errorTextOf(body, "Could not move this report."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setMoving(false);
    }
  };

  const postComment = async () => {
    if (!commentText.trim()) {
      return;
    }

    setPostingComment(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}/comments`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          Text: commentText,
          UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, JWT wins
        }),
      });

      const body = await readBody(response);

      if (response.ok) {
        setCommentText("");
        fetchReport();
      } else {
        setError(errorTextOf(body, "Could not add the comment."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setPostingComment(false);
    }
  };

  // colored badge class, same rule as the list page
  const getStatusClass = (status) => {
    if (status === "Resolved") return "report-badge--resolved";
    if (status === "Submitted") return "report-badge--submitted";
    return "report-badge--progress";
  };

  // ── the three early states: loading, failed, and loaded ──
  if (loading) {
    return <p className="report-status">Loading report...</p>;
  }

  if (error && !data) {
    return (
      <div className="detail-page">
        <p className="report-status report-status--error">{error}</p>
        <button className="btn-back" onClick={() => navigate("/report")}>
          ← Back to reports
        </button>
      </div>
    );
  }

  if (!data) {
    return null;
  }

  const report = data.Report;

  return (
    <div className="detail-page">
      {/* ── Navbar ── */}
      <nav className="report-nav">
        <div className="report-nav__brand" onClick={() => navigate("/")}>
          <div className="report-nav__logo">🏙️</div>
          <span className="report-nav__name">
            Civic<span>Fix</span>
          </span>
        </div>
        {/* ADDED — same "who is signed in" chip as the reports page, so the
            navbar does not change shape when you open a report. The name comes
            from localStorage, saved at login — no API call needed. */}
        <div className="report-nav__right">
          {localStorage.getItem("usr_FullName") && (
            <span className="report-nav__user">
              👤 {localStorage.getItem("usr_FullName")}
              <span className="report-nav__role">{role}</span>
            </span>
          )}

          <button className="report-nav__btn" onClick={() => navigate("/report")}>
            ← Back to reports
          </button>
        </div>
      </nav>

      <div className="detail-container">
        {/* any error that happened AFTER the page loaded (a failed status save) */}
        {error && <p className="report-status report-status--error">{error}</p>}

        {/* ── headline ── */}
        <div className="detail-head">
          <span className="report-item__category">{report.CategoryName}</span>
          <span className={`report-badge ${getStatusClass(report.rpt_Status)}`}>
            {report.rpt_Status}
          </span>
        </div>

        <h1 className="detail-title">{report.rpt_Title}</h1>
        <p className="detail-desc">{report.rpt_Description}</p>

        {/* ── the two photos side by side ──
            rpt_ReportedPhotoUrl is the problem, rpt_ResolvedPhotoUrl is the fix.
            Both are now real uploaded files served from the API's wwwroot/uploads,
            so they can be shown with a plain <img> tag. */}
        <div className="detail-photos">
          <div className="detail-photo">
            <p className="detail-photo__label">Reported / صورة المشكلة</p>
            {report.rpt_ReportedPhotoUrl ? (
              <img
                className="detail-photo__img"
                src={report.rpt_ReportedPhotoUrl}
                alt="The reported problem"
              />
            ) : (
              <p className="detail-photo__empty">No photo</p>
            )}
          </div>

          <div className="detail-photo">
            <p className="detail-photo__label">After the fix / صورة بعد الإصلاح</p>
            {report.rpt_ResolvedPhotoUrl ? (
              <img
                className="detail-photo__img"
                src={report.rpt_ResolvedPhotoUrl}
                alt="After the fix"
              />
            ) : (
              <p className="detail-photo__empty">Not resolved yet</p>
            )}
          </div>
        </div>

        {/* ── facts table ── */}
        <div className="detail-facts">
          <div className="detail-fact">
            <span className="detail-fact__key">Reported by</span>
            <span className="detail-fact__value">
              {report.ReporterName} ({report.ReporterRole})
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Date</span>
            <span className="detail-fact__value">
              {new Date(report.rpt_CreatedAt).toLocaleString()}
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Priority</span>
            <span className="detail-fact__value">{report.rpt_Priority || "Not set"}</span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Location</span>
            <span className="detail-fact__value">
              {report.Latitude != null
                ? `${Number(report.Latitude).toFixed(5)}, ${Number(report.Longitude).toFixed(5)}`
                : "Unknown"}
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Agreements</span>
            <span className="detail-fact__value">
              👍 {report.rpt_AgreementCount || 0} &nbsp; 👎 {report.rpt_DisagreementCount || 0}
            </span>
          </div>
        </div>

        {/* ── which baladiyat this report went to ── */}
        <h3 className="detail-section-title">🏛️ Assigned baladiyat</h3>
        <div className="detail-list">
          {data.Assignments.map((assignment, index) => (
            <div key={index} className="detail-row">
              <span>{assignment.MunicipalityName}</span>
              <span>
                {assignment.rpa_IsHandler ? "✅ handling this report" : "not handling"}
                {assignment.rpa_Points !== 0 ? ` — ${assignment.rpa_Points} pts` : ""}
              </span>
            </div>
          ))}
        </div>

        {/*
          ADDED — move this report to a different baladiye. Admin only.

          Different from the handler buttons on the Shared Reports tab: those pick
          between the baladiyat the spatial query already found, while this can
          hand the report to ANY baladiye in the country. That is what you need
          when the automatic assignment was simply wrong — a bad boundary polygon,
          a GPS reading that drifted, or a problem that is really another
          baladiye's responsibility despite where it sits.

          Without this, the only way to correct a misplaced report was to delete
          it and ask the resident to file it again, losing its comments and votes.
        */}
        {role === "Admin" && (
          <div className="detail-move">
            <h3 className="detail-section-title">↪️ Move to another baladiye</h3>

            {/* there are hundreds of baladiyat, so type to narrow the list down
                before picking — a raw dropdown of them all is unusable */}
            <input
              className="form-input"
              type="text"
              placeholder="🔍 Type to search baladiyat..."
              value={moveSearch}
              onChange={(e) => setMoveSearch(e.target.value)}
            />

            <select
              className="form-input"
              value={moveTargetId}
              onChange={(e) => setMoveTargetId(e.target.value)}
            >
              <option value="">Select a baladiye...</option>
              {municipalities
                // filter by what was typed, case-insensitively
                .filter((m) =>
                  m.mun_Name.toLowerCase().includes(moveSearch.toLowerCase()),
                )
                // cap the list so a huge dropdown does not slow the page down;
                // narrowing the search further is how you reach the rest
                .slice(0, 50)
                .map((m) => (
                  <option key={m.mun_Id} value={m.mun_Id}>
                    {m.mun_Name}
                  </option>
                ))}
            </select>

            <button
              className="btn-save-status"
              disabled={moving || !moveTargetId}
              onClick={moveReport}
            >
              {moving ? "Moving..." : "Move report"}
            </button>

            <p className="detail-vote-panel__note">
              The report leaves its current baladiye and the new one becomes
              responsible for it. If it was already resolved, the points move too.
            </p>
          </div>
        )}

        {/* ── priority votes ── */}
        <h3 className="detail-section-title">🗳️ Priority votes</h3>
        <div className="detail-votes">
          <span>🔴 High: {data.PriorityVotes.High}</span>
          <span>🟡 Medium: {data.PriorityVotes.Medium}</span>
          <span>🟢 Low: {data.PriorityVotes.Low}</span>
          <span>Total: {data.PriorityVotes.Total}</span>
        </div>

        {/*
          ADDED — a Resident votes on how urgent this report is.

          Only shown when BOTH are true:
            - I am a Resident
            - this report was submitted by a Resident
          The backend enforces the same two rules, so a hidden button and a
          rejected request would say the same thing — this just avoids offering
          a button that is guaranteed to fail.

          The report's priority becomes whichever value has the most votes, so no
          single resident decides it. One vote per person, and MyPriorityVote tells
          us whether this person has already used theirs.
        */}
        {role === "Resident" && report.ReporterRole === "Resident" && (
          <div className="detail-vote-panel">
            {/*
              CHANGED — the buttons now stay on screen after you have voted, so you
              can change your mind. Before, voting replaced them with a read-only
              line and there was no way back.

              The backend updates your existing row rather than adding a second
              one, so you still only ever have ONE vote on a report — picking a
              different option replaces your old choice.
            */}
            <p className="detail-vote-panel__ask">
              {data.MyPriorityVote
                ? "Change your vote / غيّر تصويتك"
                : "How urgent is this? / ما مدى إلحاح هذه المشكلة؟"}
            </p>

            <div className="detail-vote-panel__buttons">
              {/* these three strings must match the backend exactly —
                  VoteOnPriority rejects anything that is not Low/Medium/High */}
              {["Low", "Medium", "High"].map((option) => (
                <button
                  key={option}
                  // the option you currently hold gets the --chosen style, so you
                  // can see what you picked without a separate line of text
                  className={`btn-vote btn-vote--${option.toLowerCase()} ${
                    data.MyPriorityVote === option ? "btn-vote--chosen" : ""
                  }`}
                  // disabled only while a request is in flight, and on the option
                  // you already hold (clicking it would change nothing)
                  disabled={voting || data.MyPriorityVote === option}
                  onClick={() => votePriority(option)}
                >
                  {option === "Low" ? "🟢" : option === "Medium" ? "🟡" : "🔴"} {option}
                  {data.MyPriorityVote === option ? " ✔" : ""}
                </button>
              ))}
            </div>

            <p className="detail-vote-panel__note">
              {data.MyPriorityVote
                ? `You voted ${data.MyPriorityVote}. Pick another to change it.`
                : "One vote each."}{" "}
              The priority is whichever option has the most votes — currently{" "}
              <strong>{report.rpt_Priority || "not set"}</strong>.
            </p>
          </div>
        )}

        {/*
          ADDED — a Resident confirms the baladiye really did the work.

          Only shown when BOTH are true:
            - I am a Resident
            - this report was submitted by STAFF (i.e. it is a baladiye saying
              "we finished this job")

          This is the accountability loop. The baladiye only receives its +10
          points once THREE residents have agreed, so a baladiye cannot award
          itself points by filing reports about work it never did. That threshold
          lives in the backend (AgreeOnReport), not here.
        */}
        {role === "Resident" && report.ReporterRole === "Staff" && (
          <div className="detail-vote-panel detail-vote-panel--agree">
            {data.MyAgreement !== null && data.MyAgreement !== undefined ? (
              <p className="detail-vote-panel__done">
                {data.MyAgreement
                  ? "✔ You confirmed this work was done."
                  : "✔ You reported that this work was NOT done."}{" "}
                So far {report.rpt_AgreementCount || 0} confirmed and{" "}
                {report.rpt_DisagreementCount || 0} disputed it.
              </p>
            ) : (
              <>
                <p className="detail-vote-panel__ask">
                  Did the baladiye really do this work? / هل قامت البلدية بهذا العمل فعلاً؟
                </p>
                <div className="detail-vote-panel__buttons">
                  <button
                    className="btn-vote btn-vote--agree"
                    disabled={voting}
                    onClick={() => submitAgreement(true)}
                  >
                    👍 Yes, it was done
                  </button>
                  <button
                    className="btn-vote btn-vote--disagree"
                    disabled={voting}
                    onClick={() => submitAgreement(false)}
                  >
                    👎 No, it was not
                  </button>
                </div>
                <p className="detail-vote-panel__note">
                  The baladiye earns its points once 3 residents confirm.
                </p>
              </>
            )}
          </div>
        )}

        {/* ── the status trail ── */}
        <h3 className="detail-section-title">📜 Status history</h3>
        <div className="detail-list">
          {data.StatusHistory.length === 0 ? (
            <p className="detail-empty">No status changes yet.</p>
          ) : (
            data.StatusHistory.map((entry, index) => (
              <div key={index} className="detail-row">
                <span>
                  {entry.sth_OldStatus} → <strong>{entry.sth_NewStatus}</strong>
                </span>
                <span>
                  {entry.ChangedByName} · {new Date(entry.sth_ChangedAt).toLocaleString()}
                </span>
              </div>
            ))
          )}
        </div>

        {/* ── change status, Admin and Staff only ──
            Staff can only reach this page for their own baladiye's reports
            (the backend returns 403 otherwise), so no extra check is needed here. */}
        {canEditStatus && (
          <div className="detail-edit">
            <h3 className="detail-section-title">✎ Change status</h3>

            <select
              className="form-input"
              value={newStatus}
              onChange={(e) => setNewStatus(e.target.value)}
            >
              {STATUS_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>

            {/* the proof photo is only required when resolving */}
            {newStatus === "Resolved" && (
              <div className="form-group">
                <label className="form-label">Photo of the fix</label>
                <input
                  className="form-input"
                  type="file"
                  accept="image/*"
                  onChange={(e) => setResolvedPhotoFile(e.target.files[0])}
                />
              </div>
            )}

            <button className="btn-save-status" disabled={saving} onClick={saveStatus}>
              {saving ? "Saving..." : "Save status"}
            </button>
          </div>
        )}

        {/* ── comments ── */}
        <h3 className="detail-section-title">💬 Comments</h3>
        <div className="detail-list">
          {data.Comments.length === 0 ? (
            <p className="detail-empty">No comments yet.</p>
          ) : (
            data.Comments.map((comment) => (
              <div key={comment.cmt_Id} className="detail-comment">
                <p className="detail-comment__meta">
                  <strong>{comment.AuthorName}</strong> ({comment.AuthorRole}) ·{" "}
                  {new Date(comment.cmt_CreatedAt).toLocaleString()}
                </p>
                <p className="detail-comment__text">{comment.cmt_Text}</p>
              </div>
            ))
          )}
        </div>

        <div className="detail-add-comment">
          <textarea
            className="form-input form-textarea"
            rows="2"
            placeholder="Write a comment..."
            value={commentText}
            onChange={(e) => setCommentText(e.target.value)}
          />
          <button className="btn-save-status" disabled={postingComment} onClick={postComment}>
            {postingComment ? "Posting..." : "Post comment"}
          </button>
        </div>
      </div>
    </div>
  );
}

export default ReportDetail;
