import { BrowserRouter, Routes, Route } from 'react-router-dom';

/*
  CHANGED: the import paths below.

  components/ used to be one flat folder with 14 files in it. It is now grouped
  BY FEATURE — a folder per SCREEN, holding that screen's page plus every piece
  only that screen uses:

    components/
      auth/             LoginForm, RegisterForm
      ReportList/       ReportForm (the page) + ReportCard, ReportTabs,
                        StatusFilterBar, StaffBaladiyeBadge,
                        CreateReportForm, MapPicker
      ReportDetail/     ReportDetail (the page) + ReportStatusPanel,
                        ReportPriorityVote, MoveReportPanel, ReportComments
      ReportNavbar.jsx  SHARED by ReportList and ReportDetail, so it stays here
      Dashboard.jsx     one file, no children — no folder needed
      WelcomePage.jsx   same

  The rule: anything used by only one screen goes inside that screen's folder;
  anything used by two or more stays one level up. Nothing about how the app
  RUNS changed — only where the files sit.

  Note "./components/ReportDetail/ReportDetail" — the folder and the page inside
  it share a name. That is normal and not a typo.

  ALSO FIXED: "ReportForm,.jsx" had a stray comma in its filename, which is why
  the old import on this line ended in a comma too. It is now ReportForm.jsx.
*/
import WelcomePage from './components/WelcomePage';
import LoginForm from './components/auth/LoginForm';
import RegisterForm from './components/auth/RegisterForm';
import Dashboard from './components/Dashboard';
import ReportForm from './components/ReportList/ReportForm';
import ReportDetail from './components/ReportDetail/ReportDetail'; // ADDED: the one-report page

function App() {
  return (
    <BrowserRouter>{/*tell react well the route changes */}
      <Routes>
        <Route path="/" element={<WelcomePage />} />
        <Route path="/login" element={<LoginForm />} />
        <Route path="/register" element={<RegisterForm />} />
        <Route path="/report" element={<ReportForm/>}/>
        <Route path="/dashboard" element={<Dashboard />} />
        {/*
          FIXED: "/report" used to be listed TWICE — once above and once here.
          React Router only uses the first match, so the duplicate did nothing,
          but it is the kind of line that quietly breaks later when the two
          copies drift apart. The duplicate is replaced by the detail route below.

          ADDED: ":id" is a URL parameter. /report/7 matches this route, and
          ReportDetail reads the 7 out of it with useParams().
        */}
        <Route path="/report/:id" element={<ReportDetail />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;//Makes App available to import in main.jsx
