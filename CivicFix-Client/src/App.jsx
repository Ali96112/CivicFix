import { BrowserRouter, Routes, Route } from 'react-router-dom';

import WelcomePage from './components/WelcomePage';
import LoginForm from './components/auth/LoginForm';
import RegisterForm from './components/auth/RegisterForm';
import Dashboard from './components/Dashboard';
import ReportForm from './components/ReportForm/ReportForm';
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
