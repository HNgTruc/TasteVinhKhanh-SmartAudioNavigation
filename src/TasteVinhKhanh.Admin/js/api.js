// Mở file HTML trực tiếp (file://) nên phải ghi đầy đủ URL API
const API_BASE = 'http://localhost:5000';

async function apiCall(method, endpoint, body = null) {
    const token = localStorage.getItem('token');

    const options = {
        method,
        headers: {
            'Content-Type': 'application/json',
            ...(token && { 'Authorization': `Bearer ${token}` })
        }
    };

    if (body) options.body = JSON.stringify(body);

    const res = await fetch(`${API_BASE}${endpoint}`, options);

    if (res.status === 401) {
        logout();
        return null;
    }

    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    if (res.status === 204) return true;

    return await res.json();
}

async function login(email, password) {
    const data = await apiCall('POST', '/api/auth/login', { email, password });
    if (!data) return false;
    localStorage.setItem('token', data.accessToken);
    localStorage.setItem('userName', data.userName);
    localStorage.setItem('email', data.email);
    return true;
}

function logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    localStorage.removeItem('email');
    window.location.href = 'index.html';
}

function isLoggedIn() {
    return !!localStorage.getItem('token');
}

async function getAllPois(includeInactive = true) {
    return await apiCall('GET', `/api/poi?includeInactive=${includeInactive}`);
}

async function createPoi(data) {
    return await apiCall('POST', '/api/poi', data);
}

async function updatePoi(id, data) {
    return await apiCall('PUT', `/api/poi/${id}`, data);
}

async function deletePoi(id) {
    return await apiCall('DELETE', `/api/poi/${id}`);
}

async function upsertScript(poiId, data) {
    return await apiCall('PUT', `/api/poi/${poiId}/scripts`, data);
}

async function deleteScript(poiId, lang) {
    return await apiCall('DELETE', `/api/poi/${poiId}/scripts/${lang}`);
}

async function getSummary() {
    return await apiCall('GET', '/api/analytics/summary');
}

async function getTopPois(top = 10) {
    return await apiCall('GET', `/api/analytics/top-pois?top=${top}`);
}