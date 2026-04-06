// ── CONFIRM DIALOG ───────────────────────────────────────────────────────────

function confirmDialog(message, onConfirm, onCancel) {
    // Remove existing confirm modal if any
    const existing = document.getElementById('confirmModal');
    if (existing) existing.remove();

    const overlay = document.createElement('div');
    overlay.id = 'confirmModal';
    overlay.className = 'modal-overlay show';
    overlay.innerHTML = `
        <div class="confirm-dialog">
            <div class="confirm-icon">
                <svg width="28" height="28" fill="none" stroke="var(--warning)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><use href="#icon-alert-triangle"/></svg>
            </div>
            <div class="confirm-message">${message}</div>
            <div class="confirm-actions">
                <button class="btn btn-outline" id="confirmCancelBtn">Huỷ</button>
                <button class="btn btn-danger" id="confirmOkBtn">Xác nhận</button>
            </div>
        </div>
    `;
    document.body.appendChild(overlay);

    document.getElementById('confirmCancelBtn').onclick = () => {
        overlay.classList.remove('show');
        setTimeout(() => overlay.remove(), 220);
        if (onCancel) onCancel();
    };
    document.getElementById('confirmOkBtn').onclick = () => {
        overlay.classList.remove('show');
        setTimeout(() => overlay.remove(), 220);
        if (onConfirm) onConfirm();
    };
    overlay.addEventListener('click', e => {
        if (e.target === overlay) {
            overlay.classList.remove('show');
            setTimeout(() => overlay.remove(), 220);
            if (onCancel) onCancel();
        }
    });
}

// ── TOAST ────────────────────────────────────────────────────────────────────

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

// ── MODAL ────────────────────────────────────────────────────────────────────

function openModal(id) {
    document.getElementById(id)?.classList.add('show');
}

function closeModal(id) {
    document.getElementById(id)?.classList.remove('show');
}

document.addEventListener('click', (e) => {
    if (e.target.classList.contains('modal-overlay')) {
        e.target.classList.remove('show');
    }
});

// ── TABS ─────────────────────────────────────────────────────────────────────

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

// ── FORMAT ───────────────────────────────────────────────────────────────────

function formatDate(dateStr) {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleString('vi-VN', {
        day: '2-digit', month: '2-digit', year: 'numeric'
    });
}

function formatDateTime(dateStr) {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleString('vi-VN', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}
