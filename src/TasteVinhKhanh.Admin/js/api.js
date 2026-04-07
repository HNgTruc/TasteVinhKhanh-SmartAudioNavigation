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

// ═══════════════════════════════════════════════════════════════════════════════
// TOUR APIs
// ═══════════════════════════════════════════════════════════════════════════════

async function getTours(page = 1, pageSize = 10, search = '', includeInactive = false) {
    const params = new URLSearchParams({ page, pageSize, search: search || '', includeInactive });
    return await apiCall('GET', `/api/tour?${params}`);
}

async function getTour(id) {
    return await apiCall('GET', `/api/tour/${id}`);
}

async function createTour(data) {
    return await apiCall('POST', '/api/tour', data);
}

async function updateTour(id, data) {
    return await apiCall('PUT', `/api/tour/${id}`, data);
}

async function reorderTour(id, poiIds) {
    return await apiCall('PUT', `/api/tour/${id}/reorder`, { poiIds });
}

async function deleteTour(id) {
    return await apiCall('DELETE', `/api/tour/${id}`);
}

// ═══════════════════════════════════════════════════════════════════════════════
// VENDOR MANAGEMENT APIs  (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

/** GET /api/admin/vendors — danh sách vendor (filter ?status=Pending|Approved|Rejected) */
async function getVendors(status = '') {
    const params = status ? `?status=${encodeURIComponent(status)}` : '';
    return await apiCall('GET', `/api/admin/vendors${params}`);
}

/** GET /api/admin/vendors/:id — chi tiết 1 vendor */
async function getVendor(id) {
    return await apiCall('GET', `/api/admin/vendors/${id}`);
}

/** PUT /api/admin/vendors/:id — cập nhật trạng thái vendor */
async function updateVendor(id, data) {
    return await apiCall('PUT', `/api/admin/vendors/${id}`, data);
}

/** POST /api/admin/vendors — tạo vendor mới */
async function createVendor(data) {
    return await apiCall('POST', '/api/admin/vendors', data);
}

/** DELETE /api/admin/vendors/:id — xoá vendor */
async function deleteVendor(id) {
    const res = await fetch(`${API_BASE}/api/admin/vendors/${id}`, {
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
    });
    if (res.status === 401) { logout(); return null; }
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.message || `Xoá thất bại (HTTP ${res.status})`);
    return data;
}

/** PUT /api/admin/vendors/:vendorId/approve — duyệt vendor + gán POI */
async function approveVendor(vendorId, poiPointId = 0) {
    return await apiCall('PUT', `/api/admin/vendors/${vendorId}/approve`, { poiPointId });
}

/** PUT /api/admin/vendors/:vendorId/reject — từ chối vendor */
async function rejectVendor(vendorId, reason = '') {
    return await apiCall('PUT', `/api/admin/vendors/${vendorId}/reject`, { reason });
}

/** GET /api/admin/vendors/:id/pois — danh sách POI của vendor */
async function getVendorPois(vendorId) {
    return await apiCall('GET', `/api/admin/vendors/${vendorId}/pois`);
}

// ═══════════════════════════════════════════════════════════════════════════════
// PENDING UPDATES APIs  (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

/** GET /api/admin/pending-updates — danh sách thay đổi chờ duyệt */
async function getPendingUpdates(page = 1, pageSize = 20, status = 'Pending') {
    let url = `/api/admin/pending-updates?page=${page}&pageSize=${pageSize}`;
    if (status && status !== 'all') url += `&status=${encodeURIComponent(status)}`;
    return await apiCall('GET', url);
}

/** GET /api/admin/pending-updates/:id — chi tiết 1 pending update */
async function getPendingUpdate(id) {
    return await apiCall('GET', `/api/admin/pending-updates/${id}`);
}

/** POST /api/admin/pending-updates/:id/approve — duyệt */
async function approvePendingUpdate(id) {
    return await apiCall('POST', `/api/admin/pending-updates/${id}/approve`, {
        AdminNote: 'Approved by admin'
    });
}

/** POST /api/admin/pending-updates/:id/reject — từ chối */
async function rejectPendingUpdate(id, reason = '') {
    return await apiCall('POST', `/api/admin/pending-updates/${id}/reject`, { reason });
}

/** GET /api/admin/pending-updates/stats — badge counts */
async function getPendingStats() {
    return await apiCall('GET', '/api/admin/pending-updates/stats');
}

// ═══════════════════════════════════════════════════════════════════════════════
// STAGING IMAGE APIs
// ═══════════════════════════════════════════════════════════════════════════════

/** GET /api/admin/staging-images — danh sách ảnh chờ duyệt */
async function getStagingImages(status = 'Pending') {
    let url = '/api/admin/staging-images';
    if (status && status !== 'all') url += `?status=${encodeURIComponent(status)}`;
    return await apiCall('GET', url);
}

/** POST /api/admin/staging-images/:id/approve — duyệt ảnh */
async function approveStagingImage(id, poiPointId = 0) {
    return await apiCall('POST', `/api/admin/staging-images/${id}/approve`, { PoiPointId: poiPointId });
}

/** POST /api/admin/staging-images/:id/reject — từ chối ảnh */
async function rejectStagingImage(id, reason = '') {
    return await apiCall('POST', `/api/admin/staging-images/${id}/reject`, { reason });
}

// ═══════════════════════════════════════════════════════════════════════════════
// POI IMAGE MANAGEMENT (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

/** GET /api/admin/pois/:poiId/images — lấy gallery ảnh của một POI */
async function getPoiImageGallery(poiId) {
    return await apiCall('GET', `/api/admin/pois/${poiId}/images`);
}

/** POST /api/admin/pois/:poiId/images — thêm ảnh mới vào gallery */
async function addPoiImageApi(poiId, data) {
    return await apiCall('POST', `/api/admin/pois/${poiId}/images`, data);
}

/** DELETE /api/admin/pois/:poiId/images/:imageId — xóa trực tiếp ảnh của POI */
async function deletePoiImage(poiId, imageId) {
    return await apiCall('DELETE', `/api/admin/pois/${poiId}/images/${imageId}`);
}

// ═══════════════════════════════════════════════════════════════════════════════
// STAGING DELETION QUEUE (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

/** GET /api/admin/staging-images/deletion — danh sách yêu cầu xóa ảnh chờ duyệt */
async function getDeletionRequests(status = 'Pending') {
    let url = '/api/admin/staging-images/deletion';
    if (status && status !== 'all') url += `?status=${encodeURIComponent(status)}`;
    return await apiCall('GET', url);
}

/** POST /api/admin/staging-images/:id/approve-deletion — duyệt yêu cầu xóa ảnh */
async function approveDeletionRequest(id, adminNote = '') {
    return await apiCall('POST', `/api/admin/staging-images/${id}/approve-deletion`, { adminNote });
}

/** POST /api/admin/staging-images/:id/reject-deletion — từ chối yêu cầu xóa ảnh */
async function rejectDeletionRequest(id, reason = '') {
    return await apiCall('POST', `/api/admin/staging-images/${id}/reject-deletion`, { reason });
}
