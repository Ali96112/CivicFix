import axios from 'axios';//library used by React to send HTTP requests to your backend

const api = axios.create({
    baseURL: 'http://localhost:5140/api',// this base url well be automatically assigned for every request in backend
});

// Before Axios sends a request, run this code first.
api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');//get the token
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;//If a token exists → attaches it to the request header: Authorization: Bearer 
    }
    return config;//if no token Returns the modified config so the request continues
});

export default api;
//It's a central configuration file for all API calls — instead of writing the full URL and token setup in every component, you set it up once here and import it wherever neede