// ── TOAST NOTIFICATION ───────────────────────────────────────────────────────

function showToast(message, type = 'success') {
    let container = document.querySelector('.toast-container');
    if (!container) {
        container = document.createElement('div');
        container.className = 'toast-container';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.3s';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// ── MODAL ─────────────────────────────────────────────────────────────────────

function openModal(id) {
    document.getElementById(id).classList.add('show');
}

function closeModal(id) {
    document.getElementById(id).classList.remove('show');
}

// Đóng modal khi click ra ngoài
document.addEventListener('click', (e) => {
    if (e.target.classList.contains('modal-overlay')) {
        e.target.classList.remove('show');
    }
});

// ── TABS ──────────────────────────────────────────────────────────────────────

function initTabs(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;

    container.querySelectorAll('.tab').forEach(tab => {
        tab.addEventListener('click', () => {
            container.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            container.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
            tab.classList.add('active');
            const target = document.getElementById(tab.dataset.tab);
            if (target) target.classList.add('active');
        });
    });
}

// ── KIỂM TRA ĐĂNG NHẬP ───────────────────────────────────────────────────────

function requireAuth() {
    if (!isLoggedIn()) {
        window.location.href = 'index.html';
        return false;
    }

    // Hiển thị tên user
    const nameEl = document.getElementById('userName');
    if (nameEl) nameEl.textContent = localStorage.getItem('userName') || 'Admin';

    const avatarEl = document.getElementById('userAvatar');
    if (avatarEl) {
        const name = localStorage.getItem('userName') || 'A';
        avatarEl.textContent = name.charAt(0).toUpperCase();
    }

    return true;
}

// ── FORMAT ────────────────────────────────────────────────────────────────────

function formatDate(dateStr) {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleString('vi-VN', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function formatCoord(val) {
    return parseFloat(val).toFixed(4);
}