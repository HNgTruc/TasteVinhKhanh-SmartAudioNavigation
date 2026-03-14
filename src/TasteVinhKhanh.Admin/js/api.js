// ── CẤU HÌNH ─────────────────────────────────────────────────────────────────
const API_BASE = 'https://localhost:7001'; // Đổi thành URL server thật khi deploy

// ── HÀM GỌI API ──────────────────────────────────────────────────────────────

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

    // Token hết hạn → redirect về login
    if (res.status === 401) {
        logout();
        return null;
    }

    if (!res.ok) throw new Error(`HTTP ${res.status}`);

    // Trả về null nếu response không có body (204 No Content)
    if (res.status === 204) return true;

    return await res.json();
}

// ── AUTH ──────────────────────────────────────────────────────────────────────

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

// ── POI ───────────────────────────────────────────────────────────────────────

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

// ── ANALYTICS ─────────────────────────────────────────────────────────────────

async function getSummary() {
    return await apiCall('GET', '/api/analytics/summary');
}

async function getTopPois(top = 10) {
    return await apiCall('GET', `/api/analytics/top-pois?top=${top}`);
}