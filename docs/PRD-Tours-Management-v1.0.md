# PRD v1.0 — Tours Management Module
**Dự án:** TasteVinhKhanh — Admin Dashboard
**Phiên bản:** 1.0
**Ngày:** 2026-03-30
**Tác giả:** Claude (BA mode)
**Trạng thái:** Draft — cần review trước khi triển khai

---

## 1. Overview

### 1.1 Mục tiêu sản phẩm
Cho phép admin tạo và quản lý các **Tour** — mỗi tour là một tuyến tham quan gồm nhiều điểm POI đã được tạo từ module POIs Management, với khả năng sắp xếp thứ tự các điểm trong lộ trình.

### 1.2 Phạm vi MVP
- Admin có thể **tạo tour** từ danh sách POI đã có
- Admin có thể **sắp xếp thứ tự** các POI trong tour (kéo thả / nút lên-xuống)
- Admin có thể **chỉnh sửa** thông tin tour và danh sách POI trong tour
- Admin có thể **xóa** tour (soft delete)
- Admin có thể **xem danh sách** tất cả tour với thông tin tổng quan

### 1.3 Phạm vi Loại trừ (Out of Scope)
- Xuất/bán tour cho người dùng cuối (mobile app)
- Giao diện bản đồ trên admin (sẽ bổ sung ở v2)
- Quản lý lịch trình / thời gian bắt đầu/kết thúc tour
- Tối ưu lộ trình tự động (auto-routing)
- Giao diện mobile (MAUI app)

---

## 2. Roles & Personas

| Role | Mô tả | Quyền |
|------|--------|-------|
| Admin | Người quản trị hệ thống | CRUD Tours, CRUD POIs, xem Analytics |

---

## 3. User Stories

### US-01: Tạo tour mới
**Actor:** Admin
**Mô tả:** Admin tạo một tour mới với thông tin cơ bản và danh sách POI
**Priority:** High

**Acceptance Criteria:**
```
Given: Admin đã đăng nhập và có quyền Admin
When:  Admin nhấn nút "Tạo Tour" trên trang Tours
Then:  Hệ thống hiển thị modal tạo tour với các trường:
       - Tên tour (bắt buộc, tối đa 200 ký tự)
       - Mô tả (tùy chọn, tối đa 1000 ký tự)
       - Ngôn ngữ mặc định (VI/EN, mặc định VI)
       And: Hệ thống load danh sách POI đang active để chọn
       And: Admin chọn ít nhất 1 POI và nhấn "Lưu"
       Then: Hệ thống tạo tour, gán thứ tự POI mặc định theo thứ tự chọn,
             hiển thị toast thành công, đóng modal và refresh bảng
```

### US-02: Sắp xếp thứ tự POI trong tour
**Actor:** Admin
**Mô tả:** Admin thay đổi thứ tự các POI trong một tour
**Priority:** High

**Acceptance Criteria:**
```
Given: Admin đang xem chi tiết một tour đã có
When:  Admin nhấn nút "↑" (lên) hoặc "↓" (xuống) bên cạnh một POI trong danh sách
Then:  Hệ thống cập nhật thứ tự POI, hiển thị toast "Đã cập nhật thứ tự"
When:  Admin kéo thả (drag) một POI để thay đổi vị trí
Then:  Hệ thống cập nhật thứ tự, hiển thị toast "Đã cập nhật thứ tự"
```

### US-03: Chỉnh sửa tour
**Actor:** Admin
**Mô tả:** Admin cập nhật thông tin tour và danh sách POI
**Priority:** High

**Acceptance Criteria:**
```
Given: Admin đang xem danh sách tours
When:  Admin nhấn nút "Sửa" trên một tour
Then:  Hệ thống hiển thị modal chỉnh sửa với các trường pre-populated:
       - Tên tour, Mô tả, Ngôn ngữ (giống tạo mới)
       - Danh sách POI đã chọn với thứ tự hiện tại
       - Có thể thêm/bớt POI, thay đổi thứ tự
When:  Admin thay đổi thông tin và nhấn "Lưu"
Then:  Hệ thống cập nhật tour, hiển thị toast thành công, refresh bảng
```

### US-04: Xóa tour
**Actor:** Admin
**Mô tả:** Admin xóa một tour (soft delete)
**Priority:** Medium

**Acceptance Criteria:**
```
Given: Admin đang xem danh sách tours
When:  Admin nhấn nút "Xóa" trên một tour
Then:  Hệ thống hiển thị hộp thoại xác nhận: "Bạn có chắc muốn xóa tour '{tên tour}'?"
When:  Admin nhấn "Xóa" trên hộp thoại
Then:  Hệ thống soft-delete tour (IsActive = false), hiển thị toast "Đã xóa tour",
       refresh bảng và ẩn tour khỏi danh sách mặc định
When:  Admin nhấn "Hủy"
Then:  Hệ thống đóng hộp thoại, không thay đổi gì
```

### US-05: Xem danh sách tours
**Actor:** Admin
**Mô tả:** Admin xem toàn bộ tours đã tạo
**Priority:** High

**Acceptance Criteria:**
```
Given: Admin đã đăng nhập
When:  Admin truy cập trang Tours (tour.html)
Then:  Hệ thống hiển thị bảng gồm các cột:
       - Tên tour
       - Mô tả (cắt ngắn 100 ký tự nếu dài)
       - Số POI trong tour
       - Ngày tạo
       - Trạng thái (Hoạt động / Đã xóa)
       - Thao tác (Sửa / Xóa)
When:  Chưa có tour nào
Then:  Hệ thống hiển thị empty state: "Chưa có tour nào. Nhấn 'Tạo Tour' để bắt đầu."
When:  Admin nhấn vào tên tour
Then:  Hệ thống mở modal xem chi tiết tour với danh sách POI và thứ tự
```

### US-06: Thêm/bớt POI trong tour
**Actor:** Admin
**Mô tả:** Admin thêm POI vào tour hoặc xóa POI khỏi tour khi đang chỉnh sửa
**Priority:** High

**Acceptance Criteria:**
```
Given: Admin đang ở modal tạo/chỉnh sửa tour
When:  Admin nhấn "+ Thêm POI"
Then:  Hệ thống hiển thị danh sách POI đang active, loại trừ POI đã có trong tour
When:  Admin chọn một POI và nhấn "Thêm"
Then:  POI được thêm vào cuối danh sách tour, hiển thị toast "Đã thêm POI"
When:  Admin nhấn nút "×" bên cạnh một POI trong danh sách đang chọn
Then:  POI được xóa khỏi tour, hiển thị toast "Đã xóa POI khỏi tour"
       (nếu tour chỉ còn 1 POI thì không cho xóa, hiển thị cảnh báo)
```

---

## 4. Functional Requirements

### FR-01: Quản lý Tour cơ bản
| ID | Mô tả | Priority |
|----|--------|---------|
| FR-01.1 | Tạo tour với tên (bắt buộc), mô tả (tùy chọn) | High |
| FR-01.2 | Chỉnh sửa thông tin tour | High |
| FR-01.3 | Soft-delete tour | High |
| FR-01.4 | Xem danh sách tours (phân trang 10/trang) | High |
| FR-01.5 | Tìm kiếm tour theo tên | Medium |
| FR-01.6 | Lọc tours theo trạng thái (Hoạt động / Đã xóa) | Medium |

### FR-02: Quản lý POI trong Tour
| ID | Mô tả | Priority |
|----|--------|---------|
| FR-02.1 | Thêm POI vào tour (chọn từ danh sách POI đang active) | High |
| FR-02.2 | Xóa POI khỏi tour (giữ lại POI gốc trong hệ thống) | High |
| FR-02.3 | Sắp xếp thứ tự POI trong tour (nút ↑↓) | High |
| FR-02.4 | Sắp xếp thứ tự POI bằng kéo thả | Medium |
| FR-02.5 | Số POI tối thiểu trong tour: 1 (không bắt buộc) | Low |
| FR-02.6 | Số POI tối đa trong tour: 50 | Medium |

### FR-03: Xem chi tiết Tour
| ID | Mô tả | Priority |
|----|--------|---------|
| FR-03.1 | Xem danh sách POI trong tour theo thứ tự lộ trình | High |
| FR-03.2 | Xem thông tin tổng quan: tên, mô tả, số POI, ngày tạo, ngày cập nhật | High |
| FR-03.3 | Xem trạng thái tour (Hoạt động / Đã xóa) | Medium |

---

## 5. Non-Functional Requirements

| ID | Mô tả |
|----|--------|
| NFR-01 | **Authentication:** Tất cả API tours đều yêu cầu JWT token với role Admin |
| NFR-02 | **Validation:** Tên tour không rỗng, tối đa 200 ký tự. Mô tả tối đa 1000 ký tự |
| NFR-03 | **Authorization:** Chỉ role Admin được phép thao tác CRUD |
| NFR-04 | **Error handling:** Trả về HTTP 400 với message rõ ràng khi validation fail |
| NFR-05 | **Performance:** API danh sách tours hỗ trợ phân trang, response < 500ms với 100 tour |
| NFR-06 | **Logging:** Ghi log khi tour được tạo/sửa/xóa |
| NFR-07 | **Audit trail:** Lưu CreatedAt, UpdatedAt, CreatedBy cho mỗi tour |

---

## 6. Data Models

### 6.1 Tour Entity (Backend — `Tour.cs`)
```
Tour
├── Id: int (PK, auto-increment)
├── Name: string (required, max 200)
├── Description: string? (optional, max 1000)
├── IsActive: bool (default true)
├── CreatedAt: DateTime (UTC, auto-set)
├── UpdatedAt: DateTime? (UTC, auto-set on update)
├── CreatedBy: string (email của admin tạo)
└── TourStops: List<TourStop> (navigation property)
```

### 6.2 TourStop Entity (Backend — `TourStop.cs`)
```
TourStop
├── Id: int (PK, auto-increment)
├── TourId: int (FK → Tour.Id, required)
├── PoiId: int (FK → PoiPoint.Id, required)
├── StopOrder: int (thứ tự trong tour, 1-based)
├── CreatedAt: DateTime (UTC)
└── UpdatedAt: DateTime? (UTC)
```

### 6.3 Frontend — Tour List Item
```json
{
  "id": 1,
  "name": "Tour Miền Tây 1 Ngày",
  "description": "Khám phá các điểm du lịch nổi tiếng...",
  "poiCount": 5,
  "isActive": true,
  "createdAt": "2026-03-30T10:00:00Z",
  "updatedAt": "2026-03-30T14:30:00Z"
}
```

### 6.4 Frontend — Tour Detail
```json
{
  "id": 1,
  "name": "Tour Miền Tây 1 Ngày",
  "description": "...",
  "isActive": true,
  "pois": [
    { "poiId": 1, "poiName": "Chợ nổi Cái Răng", "stopOrder": 1 },
    { "poiId": 3, "poiName": "Vườn trái cây", "stopOrder": 2 }
  ],
  "createdAt": "...",
  "updatedAt": "..."
}
```

---

## 7. API Contracts

### 7.1 GET /api/tours
**Mô tả:** Lấy danh sách tours (phân trang)
**Auth:** Bearer JWT (Admin)
**Query params:**
| Param | Type | Mô tả |
|-------|------|--------|
| page | int | Số trang (default: 1) |
| pageSize | int | Số item/trang (default: 10, max: 50) |
| search | string | Tìm kiếm theo tên (optional) |
| includeInactive | bool | Bao gồm tours đã xóa (default: false) |

**Response 200:**
```json
{
  "items": [ TourListItem ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 10,
  "totalPages": 2
}
```

### 7.2 GET /api/tours/{id}
**Mô tả:** Lấy chi tiết một tour kèm danh sách POI
**Auth:** Bearer JWT (Admin)
**Response 200:** TourDetail (như 6.4)
**Response 404:** `{ "error": "Tour không tồn tại" }`

### 7.3 POST /api/tours
**Mô tả:** Tạo tour mới
**Auth:** Bearer JWT (Admin)
**Request body:**
```json
{
  "name": "string (required)",
  "description": "string (optional)",
  "poiIds": [ int ] // danh sách PoiId, theo thứ tự muốn sắp xếp
}
```
**Response 201:** TourDetail
**Response 400:** `{ "error": "Tên tour không được để trống" }`

### 7.4 PUT /api/tours/{id}
**Mô tả:** Cập nhật tour (thông tin + POIs)
**Auth:** Bearer JWT (Admin)
**Request body:**
```json
{
  "name": "string (required)",
  "description": "string (optional)",
  "poiIds": [ int ] // thay thế toàn bộ danh sách POI
}
```
**Response 200:** TourDetail
**Response 400:** Validation error
**Response 404:** Tour không tồn tại

### 7.5 PUT /api/tours/{id}/reorder
**Mô tả:** Cập nhật thứ tự POI trong tour (chỉ thay đổi StopOrder)
**Auth:** Bearer JWT (Admin)
**Request body:**
```json
{
  "poiIds": [ int ] // danh sách PoiId theo thứ tự mới
}
```
**Response 200:** TourDetail
**Response 400:** `poiIds` không khớp với danh sách POI hiện có trong tour

### 7.6 DELETE /api/tours/{id}
**Mô tả:** Soft-delete tour
**Auth:** Bearer JWT (Admin)
**Response 204:** No content
**Response 404:** Tour không tồn tại

---

## 8. Dependencies & Risks

### Dependencies
| Dep | Mô tả |
|-----|--------|
| POIs Management | Module POIs phải hoạt động để chọn POI vào tour |
| Auth System | JWT phải hoạt động để bảo vệ API |
| SyncService (MauiApp) | Cân nhắc: MauiApp sync có cần lấy danh sách tours không? (MVP: không) |

### Risks
| Risk | Mô tả | Mitigation |
|------|--------|-----------|
| R01 | POI bị xóa sau khi đã thêm vào tour → tour bị hỏng | Khi xóa POI, kiểm tra có tour nào chứa POI đó không → cảnh báo |
| R02 | Nhiều admin cùng sửa một tour → conflict | Thêm Optimistic Concurrency (Version/UpdatedAt check) |
| R03 | Tour chỉ có 1 POI → trải nghiệm kém | Thêm validation: cảnh báo nếu < 2 POI |

---

## 9. Open Questions / Assumptions

| # | Câu hỏi | Assumption (nếu chưa rõ) |
|---|---------|--------------------------|
| OQ-01 | MauiApp có cần sync tours không? | **Giả định:** MVP không, chỉ sync POIs. Tours quản lý riêng trên admin. |
| OQ-02 | Admin có cần duplicate/copy tour không? | **Giả định:** Không, MVP loại trừ. |
| OQ-03 | Tour có hỗ trợ đa ngôn ngữ (như POI audio script) không? | **Giả định:** Không ở MVP. Tên/mô tả tour chỉ có 1 ngôn ngữ (VI). |
| OQ-04 | Có giới hạn số tour không? | **Giả định:** Không giới hạn. |
| OQ-05 | POI đã inactive có hiển thị trong tour detail không? | **Giả định:** Có hiển thị (giữ nguyên trong TourStop), nhưng đánh dấu "(Đã ẩn)" trên UI. |
| OQ-06 | Sort order mặc định khi tạo tour? | **Giả định:** Theo thứ tự POI được chọn trong danh sách. |

---

## 10. Future Enhancements (Post-MVP)
- Giao diện bản đồ trên admin để visualize tour route
- Tối ưu lộ trình tự động (TSP solver)
- Lịch trình tour (thời gian bắt đầu/kết thúc dự kiến)
- Xuất tour cho người dùng cuối (giao diện mobile)
- Duplicate/copy tour
- Tour đa ngôn ngữ
- Bulk actions (activate/deactivate nhiều tour)

---

## 11. DoD (Definition of Done) cho Step 4 — POC

Trước khi coi là hoàn thành, tất cả các điều sau phải đạt:

- [ ] API CRUD tours trả về đúng theo contract
- [ ] JWT auth bảo vệ tất cả endpoints
- [ ] Soft-delete hoạt động đúng
- [ ] TourStop (thứ tự POI) hoạt động đúng sau khi tạo/sửa/reorder
- [ ] Frontend: bảng danh sách tours hiển thị đúng
- [ ] Frontend: modal tạo/sửa tour hoạt động đúng
- [ ] Frontend: sắp xếp thứ tự POI bằng nút ↑↓ hoạt động
- [ ] Frontend: toast notification hiển thị đúng
- [ ] Frontend: empty state khi chưa có tour
- [ ] Frontend: loading state khi fetch API
- [ ] Frontend: error state khi API lỗi
- [ ] Build thành công (no compile errors)
- [ ] Đã kiểm thử end-to-end: Tạo tour → Thêm POI → Sắp xếp → Sửa → Xóa
