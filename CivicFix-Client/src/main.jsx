import { StrictMode } from 'react'//a tool that helps catch bugs during development
import { createRoot } from 'react-dom/client'//It takes a real HTML element and tells React "render everything here."
import 'bootstrap/dist/css/bootstrap.min.css'
import './styles/theme.css'
import './index.css'           //Imports CSS files globally
import App from './App.jsx'//imports your main App component — the one that contains all your routes and pages.

createRoot(document.getElementById('root')).render(//finds the <div id="root"> in index.html — the single HTML element your entire React app lives inside
  <StrictMode>
    <App />
  </StrictMode>,//renders your entire app inside it, wrapped in StrictMode for development checks
)