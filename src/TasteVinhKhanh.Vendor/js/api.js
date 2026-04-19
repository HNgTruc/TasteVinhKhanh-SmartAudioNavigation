// Vendor Portal API + Auth — kết nối đến TasteVinhKhanh API
// Tất cả trang đều load file này TRƯỚC script riêng.

const API_BASE = "http://localhost:5000";

// ── HTTP helper ───────────────────────────────────────────────────────────

async function apiCall(method, endpoint, body = null) {
  const token = localStorage.getItem("vendorToken");
  const options = {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(token && { Authorization: `Bearer ${token}` }),
    },
  };
  if (body) options.body = JSON.stringify(body);

  const url = `${API_BASE}${endpoint}`;
  console.log(`📤 ${method} ${url}`, { token: !!token, body });

  const res = await fetch(url, options);
  console.log(`📥 Response ${res.status}:`, res.statusText);

  if (res.status === 401) {
    vendorLogout();
    return null;
  }

  if (!res.ok) {
    let errorMsg = `HTTP ${res.status}`;
    try {
      const errData = await res.json();
      errorMsg = errData.message || errorMsg;
    } catch (e) {
      // Nếu response không phải JSON, dùng status text
      errorMsg = res.statusText || errorMsg;
    }
    console.error(`❌ Error: ${errorMsg}`);
    throw new Error(errorMsg);
  }

  if (res.status === 204) return true;
  return await res.json();
}

// ── AUTH ──────────────────────────────────────────────────────────────────

async function vendorRegister(
  businessName,
  ownerName,
  email,
  password,
  phone,
  address,
) {
  try {
    const res = await fetch(`${API_BASE}/api/auth/vendor-register`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        businessName,
        ownerName,
        email,
        password,
        phone,
        address: address || "",
      }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || `HTTP ${res.status}`);
    return { success: true, message: data.message };
  } catch (err) {
    return { success: false, message: err.message };
  }
}

async function vendorLogin(email, password) {
  try {
    const res = await fetch(`${API_BASE}/api/auth/vendor-login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });

    if (res.status === 403) {
      const data = await res.json();
      throw new Error(data.message || "Tài khoản đang chờ được duyệt.");
    }

    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      throw new Error(data.message || "Đăng nhập thất bại.");
    }

    const data = await res.json();
    localStorage.setItem("vendorToken", data.accessToken);
    localStorage.setItem("vendorName", data.userName || email);
    localStorage.setItem("vendorEmail", data.email || email);
    return true;
  } catch (err) {
    throw err;
  }
}

async function vendorForgotPassword(email, phone, newPassword) {
  try {
    const res = await fetch(`${API_BASE}/api/auth/vendor-forgot-password`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, phone, newPassword }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok)
      throw new Error(data.message || "Không thể khôi phục mật khẩu.");
    return {
      success: true,
      message: data.message || "Khôi phục mật khẩu thành công.",
    };
  } catch (err) {
    return {
      success: false,
      message: err.message || "Khôi phục mật khẩu thất bại.",
    };
  }
}

function vendorLogout() {
  localStorage.removeItem("vendorToken");
  localStorage.removeItem("vendorName");
  localStorage.removeItem("vendorEmail");
  window.location.href = "index.html";
}

function isVendorLoggedIn() {
  return !!localStorage.getItem("vendorToken");
}

function requireVendorAuth() {
  if (!isVendorLoggedIn()) {
    window.location.href = "index.html";
    return false;
  }
  const nameEl = document.getElementById("userName");
  if (nameEl)
    nameEl.textContent = localStorage.getItem("vendorName") || "Vendor";
  const avatarEl = document.getElementById("userAvatar");
  if (avatarEl) {
    const name = localStorage.getItem("vendorName") || "V";
    avatarEl.textContent = name.charAt(0).toUpperCase();
  }
  return true;
}

// ── VENDOR PROFILE ────────────────────────────────────────────────────────

async function getVendorProfile() {
  return await apiCall("GET", "/api/vendor/profile");
}

// ── POI (Vendor chỉ thấy POI thuộc vendor đó) ──────────────────────────────

async function getMyPois() {
  return await apiCall("GET", "/api/vendor/pois");
}

async function getMyPoi(id) {
  return await apiCall("GET", `/api/vendor/pois/${id}`);
}

async function createMyPoi(data) {
  return await apiCall("POST", "/api/vendor/pois", data);
}

async function updateMyPoi(id, data) {
  return await apiCall("PUT", `/api/vendor/pois/${id}`, data);
}

// ── AUDIO SCRIPTS ───────────────────────────────────────────────────────────

async function getMyScripts(poiId) {
  return await apiCall("GET", `/api/vendor/pois/${poiId}/scripts`);
}

async function upsertMyScript(poiId, data) {
  return await apiCall("PUT", `/api/vendor/pois/${poiId}/scripts`, data);
}

/** GET /api/audio/:scriptId — lấy audio (cần token) */
function getAudioUrl(scriptId) {
  return `${API_BASE}/api/audio/${scriptId}`;
}

// ── IMAGE UPLOAD (DIRECT — upload thẳng lên, không qua duyệt) ─────────────

async function uploadImage(file, poiId) {
  const token = localStorage.getItem("vendorToken");
  const formData = new FormData();
  formData.append("files", file);
  if (poiId) formData.append("poiId", poiId);

  const res = await fetch(`${API_BASE}/api/vendor/images/upload`, {
    method: "POST",
    headers: {
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: formData,
  });

  if (res.status === 401) {
    vendorLogout();
    return null;
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.message || `Upload failed: HTTP ${res.status}`);
  }

  const data = await res.json();
  return { success: true, imageUrl: data.urls?.[0], message: data.message };
}

// ── IMAGE STAGING (chờ duyệt) ───────────────────────────────────────────────

/**
 * Upload ảnh vào thư mục staging (chờ admin duyệt).
 * Trả về tempUrl để gửi kèm trong PendingPOIUpdate.
 */
async function uploadImageForApproval(file, poiId) {
  const token = localStorage.getItem("vendorToken");
  const formData = new FormData();
  formData.append("file", file);
  formData.append("poiId", poiId);

  const res = await fetch(`${API_BASE}/api/vendor/images/staging`, {
    method: "POST",
    headers: {
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: formData,
  });

  if (res.status === 401) {
    vendorLogout();
    return null;
  }
  const data = await res.json().catch(() => ({}));
  if (!res.ok)
    throw new Error(data.message || `Upload failed: HTTP ${res.status}`);

  return { success: true, tempUrl: data.tempUrl, stagingId: data.stagingId };
}

// ── DELETE IMAGE REQUEST ─────────────────────────────────────────────────────

/**
 * Gửi yêu cầu xóa ảnh (chờ admin duyệt).
 * @param {number} imageId - Id của ảnh trong RestaurantImages
 * @param {number} poiPointId - POI của ảnh đó
 */
async function requestDeleteImage(imageId, poiPointId) {
  return await apiCall("POST", "/api/vendor/images/delete-request", {
    imageId,
    poiPointId,
  });
}

// ── UPDATE HISTORY ─────────────────────────────────────────────────────────

async function getMyUpdates() {
  return await apiCall("GET", "/api/vendor/updates");
}

// ── ANALYTICS ──────────────────────────────────────────────────────────────

async function getVendorSummary() {
  return await apiCall("GET", "/api/vendor/analytics/summary");
}

// ── LOGO UPLOAD ───────────────────────────────────────────────────────────

/**
 * Upload logo quán (chờ admin duyệt).
 * @param {File} file - File ảnh logo
 * @returns {{ success, tempUrl, stagingId }}
 */
async function uploadLogoForApproval(file) {
  const token = localStorage.getItem("vendorToken");
  const formData = new FormData();
  formData.append("file", file);

  const res = await fetch(`${API_BASE}/api/vendor/logo/upload`, {
    method: "POST",
    headers: {
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: formData,
  });

  if (res.status === 401) {
    vendorLogout();
    return null;
  }
  const data = await res.json().catch(() => ({}));
  if (!res.ok)
    throw new Error(data.message || `Upload failed: HTTP ${res.status}`);
  return { success: true, tempUrl: data.tempUrl, stagingId: data.stagingId };
}

/**
 * Gửi yêu cầu xóa logo hiện tại (chờ admin duyệt).
 * @returns {{ success, stagingId }}
 */
async function requestDeleteLogo() {
  const token = localStorage.getItem("vendorToken");
  const res = await fetch(`${API_BASE}/api/vendor/logo/delete`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(token && { Authorization: `Bearer ${token}` }),
    },
  });
  if (res.status === 401) {
    vendorLogout();
    return null;
  }
  const data = await res.json().catch(() => ({}));
  if (!res.ok)
    throw new Error(data.message || `Request failed: HTTP ${res.status}`);
  return { success: true, stagingId: data.stagingId };
}

// ── BILLING ──────────────────────────────────────────────────────────────

async function submitVendorPayment({ paymentId, bankName, transactionId, vendorNote, receiptFile }) {
  const token = localStorage.getItem("vendorToken");
  const formData = new FormData();
  formData.append("paymentId", paymentId);
  formData.append("bankName", bankName || "");
  formData.append("transactionId", transactionId || "");
  formData.append("vendorNote", vendorNote || "");
  formData.append("receipt", receiptFile);

  const res = await fetch(`${API_BASE}/api/vendor/payments/submit`, {
    method: "POST",
    headers: {
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: formData,
  });

  if (res.status === 401) {
    vendorLogout();
    return null;
  }

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(data.message || `Submit payment failed: HTTP ${res.status}`);
  }

  return data;
}

async function getMyPaymentHistory() {
  return await apiCall("GET", "/api/vendor/payments/history");
}
