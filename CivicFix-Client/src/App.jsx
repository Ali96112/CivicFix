import { BrowserRouter, Routes, Route } from 'react-router-dom';
import WelcomePage from './components/WelcomePage';
import LoginForm from './components/LoginForm';
import RegisterForm from './components/RegisterForm';
import Dashboard from './components/Dashboard';
import ReportForm from './components/ReportForm,';
import ReportDetail from './components/ReportDetail'; // ADDED: the one-report page

function App() {
  return (
    <BrowserRouter>{/*tell react well the route changes */}
      <Routes>
        <Route path="/" element={<WelcomePage />} />
        <Route path="/login" element={<LoginForm />} />
        <Route path="/register" element={<RegisterForm />} />
        <Route path="/report" element={<ReportForm/>}/>
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/report/:id" element={<ReportDetail />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;//Makes App available to import in main.jsx
