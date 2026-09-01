import { useState, useEffect } from "react"; 
import "../styles/Dashboard.css";
import Navbar from "./layout/Navbar";

function Dashboard() {

  const [municipalities, setMunicipalities] = useState([]); // holds all baladiyat from backend — starts as empty array
  const [search, setSearch] = useState(""); // holds what user typed in search box — starts empty
  const [loading, setLoading] = useState(true); // true while fetching data from backend
  const [error, setError] = useState(""); // error message if fetch fails

  useEffect(() => {
    // runs automatically when the page first loads
    const fetchMunicipalities = async () => {
      //waiting without frezzing
      try {
        const response = await fetch(
          "http://localhost:5140/api/Municipalities", //this url goes to getDashboard
        ); // GET request — no login needed
        if (response.ok) {
          const data = await response.json(); // convert JSON to JavaScript array
          setMunicipalities(data); // the function set the data in Municipalities
        } else {
          setError("Failed to load municipalities.");
        }
      } catch (err) {
        setError("Could not connect to server.");
      } finally {
        setLoading(false); // turn off loading whether success or error
      }
    };

    fetchMunicipalities(); // call the function
  }, []); // [] means run only once — when the page first loads

  // filter municipalities by search input — no new fetch needed
  const filtered = municipalities
    .map((m, i) => ({ ...m, originalRank: i + 1 })) //adds originalRank to every item before filtering so rank never changes
    .filter((m) => m.mun_Name.toLowerCase().includes(search.toLowerCase())) //keep baladeye that match the search with conversion is searched in lower case
    .slice(0, 20); //show only 20 items

  // decide what to show
  let statusMessage = null;

  if (loading) {
    statusMessage = <p className="dashboard-status">Loading baladiyat...</p>;
  }

  if (error) {
    statusMessage = (
      <p className="dashboard-status dashboard-status--error">{error}</p>
    );
  }

  if (!loading && filtered.length === 0) {
    statusMessage = (
      <p className="dashboard-status">No baladiye found matching "{search}"</p>
    );
  }

  return (
    <div className="dashboard-page">
      {" "}
       <Navbar />

      {/* ── Header ── */}
      <div className="dashboard-header">
        <h1 className="dashboard-header__title">🏆 Baladiye Leaderboard</h1>
        <p className="dashboard-header__sub">
          لوحة شرف البلديات — ranked by resolved issues
        </p>

        {/* search input */}
        <input
          className="dashboard-search"
          type="text"
          placeholder="🔍 Search for a baladiye..."
          value={search} //value shown in text field comes from state
          onChange={(e) => setSearch(e.target.value)} // updates search state on every keystroke
        />
      </div>
      {/* ── Leaderboard ── */}
      <div className="dashboard-list">
        {statusMessage}{" "}
        {/* shows loading, error, or no results — set before return */}
        {filtered.map((mun, i) => ( //loooooop
          <div key={mun.mun_Id} className="dashboard-item">
            {/* rank number or medal */}
            <div className="dashboard-item__rank">
              {mun.originalRank === 1//is it 1? → show 🥇
                ? "🥇"
                : mun.originalRank === 2//is it 2? → show 🥈
                  ? "🥈"
                  : mun.originalRank === 3
                    ? "🥉"
                    : `#${mun.originalRank}`}{/*show #4, #5, etc. */}
            </div>

            {/* baladiye name — truncated to 15 chars */}
            <div className="dashboard-item__name">
              {mun.mun_Name.length > 15
                ? mun.mun_Name.substring(0, 15) + "..."
                : mun.mun_Name}
            </div>

            {/* points bar */}
            <div className="dashboard-item__bar-wrap">
              <div
                className="dashboard-item__bar"
                style={{
                  width: `${Math.min((mun.mun_TotalPoints / (municipalities[0]?.mun_TotalPoints || 1)) * 100, 100)}%`,
                }}
              />
            </div>

            {/* points number */}
            <div className="dashboard-item__points">
              {mun.mun_TotalPoints} pts
            </div>
          </div>
        ))}{/*end looooooop */}
      </div>
      {/* end dashboard-list */}
    </div> // end dashboard-page
  );
}

export default Dashboard;
