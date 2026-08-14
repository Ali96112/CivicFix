import axios from 'axios';//toll for http request for backend

const api = axios.create({
    baseURL: 'http://localhost:5140/api',// this base url well be automatically assigned for every request in backend
});

// automatically attach JWT token to every request
api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');//Reads the JWT token from localStorage (where React stores it after login
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;//If a token exists → attaches it to the request header: Authorization: Bearer 
    }
    return config;//Returns the modified config so the request continues
});

export default api;
//It's a central configuration file for all API calls — instead of writing the full URL and token setup in every component, you set it up once here and import it wherever neede