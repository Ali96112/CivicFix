import { BrowserRouter, Routes, Route } from "react-router-dom";

import WelcomePage from "./components/WelcomePage";
import LoginForm from "./components/auth/LoginForm";
import RegisterForm from "./components/auth/RegisterForm";
import Dashboard from "./components/Dashboard";
import ReportForm from "./components/ReportForm/ReportForm";
import ReportDetail from "./components/ReportDetail/ReportDetail";
import ForgotPassword from "./components/auth/ForgotPassword";
import ResetPassword from "./components/auth/ResetPassword";
import About from "./components/info/About";
import Features from "./components/info/Features";
import Contact from "./components/info/Contact";
import Privacy from "./components/info/Privacy";

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<WelcomePage />} />
        <Route path="/login" element={<LoginForm />} />
        <Route path="/register" element={<RegisterForm />} />
        <Route path="/report" element={<ReportForm />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/forgot-password" element={<ForgotPassword />} />
        <Route path="/reset-password" element={<ResetPassword />} />
        <Route path="/report/:id" element={<ReportDetail />} />
        <Route path="/about" element={<About />} />
        <Route path="/features" element={<Features />} />
        <Route path="/contact" element={<Contact />} />
        <Route path="/privacy" element={<Privacy />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;