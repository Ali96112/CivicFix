import { useState } from "react";
import { readBody, errorTextOf } from "../../services/apiHelpers";

function ReportComments({ reportId, comments, onPosted }) {
  const [commentText, setCommentText] = useState("");
  const [postingComment, setPostingComment] = useState(false);
  const [error, setError] = useState("");

  // POST api/Reports/{id}/comments  →  ReportsFeedbackController.AddComment
  const postComment = async () => {
    // an empty comment is not worth a round trip
    if (!commentText.trim()) {
      return;
    }

    setPostingComment(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${reportId}/comments`,//for posting comment
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            Text: commentText,
            UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, the JWT wins
          }),
        },
      );

      const body = await readBody(response);

      if (response.ok) {
        setCommentText(""); // clear the box only on success, so a failed post
        // does not lose what the user typed
        onPosted();
      } else {
        setError(errorTextOf(body, "Could not add the comment."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setPostingComment(false);
    }
  };

  return (
    <>
      <h3 className="detail-section-title">💬 Comments</h3>

      <div className="detail-list">
        {comments.length === 0 ? (
          <p className="detail-empty">No comments yet.</p>
        ) : (
          comments.map((comment) => (//coment come here as prop list from report detail
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
        <button
          className="btn-save-status"
          disabled={postingComment}
          onClick={postComment}
        >
          {postingComment ? "Posting..." : "Post comment"}
        </button>

        {error && <p className="report-status report-status--error">{error}</p>}
      </div>
    </>
  );
}

export default ReportComments;
