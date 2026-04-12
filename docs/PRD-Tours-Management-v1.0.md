# Tổng quan ứng dụng thuyết minh tự động – Phố ẩm thực Vĩnh Khánh

---

## 1. Giới thiệu

### 1.1 Tổng quan dự án

**TasteVinhKhanh — Smart Audio Navigation** là hệ thống ứng dụng di động (MAUI) kết hợp cổng quản trị web (Admin Portal) và cổng nhà hàng (Vendor Portal), phục vụ cho khu phố ẩm thực Vĩnh Khánh, Quận 4, TP.HCM.

Ứng dụng cho phép người dùng di chuyển dọc tuyến phố ẩm thực Vĩnh Khánh và tự động phát audio thuyết minh khi đến gần các điểm quán ăn (POI). Hệ thống hỗ trợ đa ngôn ngữ (VI/EN) cho nội dung audio và quản lý tập trung qua backend API.

### 1.2 Bối cảnh

| Thành phần | Công nghệ | Vai trò |
|-----------|-----------|---------|
| `TasteVinhKhanh.Api` | ASP.NET Core 10 Web API | REST API backend — xác thực, quản lý POI/Tour/Vendor, audio playback, sync |
| `TasteVinhKhanh.Admin` | Static HTML/JS | Dashboard quản trị — quản lý POI, duyệt ảnh, quản lý tour |
| `TasteVinhKhanh.Vendor` | Static HTML/JS | Cổng thông tin nhà hàng — upload ảnh, chỉnh sửa thông tin |
| `TasteVinhKhanh.MauiApp` | .NET MAUI (iOS/Android) | Ứng dụng di động — phát audio khi đến gần POI, xem tour, điều hướng |
| `TasteVinhKhanh.Shared` | .NET class library | Entity models + DTOs dùng chung cho Api và MauiApp |

### 1.3 Kiến trúc hệ thống

```
┌─────────────────────┐      ┌──────────────────────┐
│   MauiApp (MAUI)    │      │ Admin Portal (HTML)  │
│   iOS / Android     │      │ Vendor Portal (HTML) │
└────────┬────────────┘      └──────────┬───────────┘
         │  REST / JWT                  │  REST / JWT
         ▼                              ▼
┌──────────────────────────────────────────────────┐
│              TasteVinhKhanh.Api                  │
│           ASP.NET Core 10 Web API                │
│  ┌──────────┐ ┌──────────┐ ┌──────────────────┐  │
│  │ Auth JWT │ │ POI Mgmt │ │ Vendor Mgmt      │  │
│  └──────────┘ └──────────┘ └──────────────────┘  │
│  ┌──────────────────────────────────────────┐    │
│  │         Tours Management (this PRD)      │    │
│  └──────────────────────────────────────────┘    │
└──────────────────────┬───────────────────────────┘
                       │ EF Core / SQL Server
                       ▼
                ┌──────────────┐
                │ SQL Server   │
                │ (thủ công)   │
                └──────────────┘
```

---

## 2. Mục tiêu hệ thống

### 2.1 Mục tiêu chung

Module **Tours Management** cho phép admin tạo và quản lý các **Tour** — mỗi tour là một tuyến tham quan gồm nhiều điểm POI đã được tạo từ module POIs Management, với khả năng sắp xếp thứ tự các điểm trong lộ trình.

### 2.2 Mục tiêu cụ thể

| # | Mục tiêu | Chi tiết |
|---|---------|---------|
| M-01 | Quản lý tour trọn vòng đời | Admin có thể tạo, xem, chỉnh sửa, xóa tour |
| M-02 | Sắp xếp lộ trình POI | Admin sắp xếp thứ tự các điểm dừng trong tour |
| M-03 | Tích hợp với POI hiện có | Tour chọn POI từ hệ thống POIs đang hoạt động |
| M-04 | Kiểm soát trạng thái | Soft-delete để không mất dữ liệu lịch sử |
| M-05 | Audit trail | Lưu lại ai tạo, lúc nào, cập nhật lúc nào |

### 2.3 Các chỉ số đo lường (KPIs)

| Chỉ số | Mục tiêu |
|--------|----------|
| Số lượng tour | Không giới hạn |
| Số POI/tour | 1 – 50 POI |
| Thời gian tạo tour | < 2 giây (từ khi nhấn Lưu) |
| API response time | < 500ms với 100 tour |
| Phân trang | Mặc định 10/trang, tối đa 50 |

---

## 3. Phạm vi hệ thống

### 3.1 Phạm vi trong (In Scope) — MVP

- Admin tạo tour mới từ danh sách POI đang hoạt động
- Admin xem danh sách tất cả tour (phân trang, tìm kiếm, lọc trạng thái)
- Admin xem chi tiết tour kèm danh sách POI theo thứ tự lộ trình
- Admin chỉnh sửa tour (thông tin + toàn bộ POI)
- Admin sắp xếp lại thứ tự POI trong tour
- Admin xóa tour (soft delete)
- Audit trail: CreatedBy, CreatedAt, UpdatedAt

### 3.2 Phạm vi ngoài (Out of Scope)

- Xuất/bán tour cho người dùng cuối (MauiApp) — MauiApp chỉ sync POIs, không sync Tours
- Giao diện bản đồ trên admin
- Quản lý lịch trình / thời gian bắt đầu/kết thúc tour
- Tối ưu lộ trình tự động (auto-routing)
- Duplicate/copy tour
- Restore tour đã xóa (soft-delete)
- Hỗ trợ đa ngôn ngữ cho tên/mô tả tour
- Bulk actions (activate/deactivate nhiều tour cùng lúc)
- Giới hạn số tour được tạo
- Optimistic concurrency (xử lý conflict khi nhiều admin sửa cùng lúc)

### 3.3 Ràng buộc hệ thống

| Ràng buộc | Chi tiết |
|-----------|---------|
| Công nghệ Backend | ASP.NET Core 10 Web API, Entity Framework Core 10 |
| Cơ sở dữ liệu | SQL Server — tạo thủ công trong SSMS, không qua EF migrations |
| Xác thực | JWT Bearer token, role Admin |
| Ngôn ngữ | Tiếng Việt (VI) cho tên/mô tả tour |
| Audio | TTS audio chỉ gắn ở tầng POI, không gắn ở tầng Tour |

---

## 4. Roles & Personas

| Role | Mô tả | Quyền |
|------|--------|-------|
| Admin | Người quản trị hệ thống | CRUD Tours, xem chi tiết, sắp xếp POI |

> **Ghi chú:** Tour không gắn với Vendor — không có endpoint dành cho Vendor quản lý Tour. Tour là đối tượng thuần túy do Admin quản lý.

---

## 5. User Stories

### US-01: Tạo tour mới
**Actor:** Admin
**Priority:** High

**Acceptance Criteria:**
```
Given:  Admin đã đăng nhập (JWT với role Admin)
When:   Admin nhấn nút "Tạo Tour" trên trang Tours
Then:   Hệ thống hiển thị modal tạo tour với:
        - Tên tour (bắt buộc, tối đa 200 ký tự)
        - Mô tả (tùy chọn, tối đa 1000 ký tự)
        - Danh sách POI đang active để chọn (tối đa 50 POI)
        - Thứ tự POI mặc định theo thứ tự chọn trong danh sách
And:    Admin nhập tên, mô tả, chọn ít nhất 1 POI và nhấn "Lưu"
Then:   Hệ thống tạo tour, trả về HTTP 201, hiển thị toast thành công,
        refresh bảng danh sách
```

### US-02: Xem danh sách tours
**Actor:** Admin
**Priority:** High

**Acceptance Criteria:**
```
Given:  Admin đã đăng nhập
When:   Admin truy cập trang Tours (tour.html)
Then:   Hệ thống hiển thị bảng gồm các cột:
        - Tên tour
        - Mô tả (cắt ngắn 100 ký tự nếu dài)
        - Số POI trong tour
        - Trạng thái (Hoạt động / Đã xóa)
        - Ngày tạo / Ngày cập nhật
        - Thao tác (Sửa / Xóa)
And:    Hệ thống hỗ trợ phân trang (mặc định 10/trang), tìm kiếm theo tên,
        lọc theo trạng thái (bao gồm đã xóa)
When:   Chưa có tour nào
Then:   Hiển thị empty state: "Chưa có tour nào. Nhấn 'Tạo Tour' để bắt đầu."
```

### US-03: Xem chi tiết tour
**Actor:** Admin
**Priority:** High

**Acceptance Criteria:**
```
Given:  Admin đang xem danh sách tours
When:   Admin nhấn vào tên tour hoặc nút "Xem chi tiết"
Then:   Hệ thống hiển thị modal chi tiết tour với:
        - Tên, mô tả, trạng thái, ngày tạo/cập nhật
        - Danh sách POI theo thứ tự lộ trình (StopOrder)
        - POI đã bị ẩn hiển thị kèm đánh dấu "(Đã ẩn)"
```

### US-04: Sắp xếp thứ tự POI trong tour
**Actor:** Admin
**Priority:** High

**Acceptance Criteria:**
```
Given:  Admin đang xem chi tiết một tour đã có
When:   Admin gửi PUT /api/tour/{id}/reorder với danh sách POIIds theo thứ tự mới
Then:   Hệ thống cập nhật StopOrder cho từng POI, trả về TourDetail mới
        hiển thị toast "Đã cập nhật thứ tự"
When:   Danh sách POIIds không khớp với các POI hiện có trong tour
Then:   Hệ thống trả về HTTP 400: "Danh sách POI không khớp với các điểm hiện có trong tour."
```

### US-05: Chỉnh sửa tour
**Actor:** Admin
**Priority:** High

**Acceptance Criteria:**
```
Given:  Admin đang xem danh sách tours
When:   Admin nhấn nút "Sửa" trên một tour
Then:   Hệ thống hiển thị modal chỉnh sửa pre-populated:
        - Tên, mô tả
        - Danh sách POI đã chọn với thứ tự hiện tại
        - Có thể thêm/bớt POI, thay đổi thứ tự
When:   Admin thay đổi thông tin và nhấn "Lưu"
Then:   Hệ thống thay thế toàn bộ danh sách POI (recreate TourStops),
        trả về TourDetail mới, hiển thị toast thành công
```

### US-06: Xóa tour
**Actor:** Admin
**Priority:** Medium

**Acceptance Criteria:**
```
Given:  Admin đang xem danh sách tours
When:   Admin nhấn nút "Xóa" trên một tour
Then:   Hệ thống hiển thị hộp thoại xác nhận: "Bạn có chắc muốn xóa tour '{tên tour}'?"
When:   Admin nhấn "Xóa" trên hộp thoại
Then:   Hệ thống soft-delete (IsActive = false), HTTP 204,
        refresh bảng, tour không còn hiển thị mặc định
When:   Admin nhấn "Hủy"
Then:   Hệ thống đóng hộp thoại, không thay đổi gì
```

---

## 6. Functional Requirements

### FR-01: Quản lý Tour cơ bản

| ID | Mô tả | Priority |
|----|--------|----------|
| FR-01.1 | Tạo tour với tên (bắt buộc, tối đa 200 ký tự), mô tả (tùy chọn, tối đa 1000 ký tự) | High |
| FR-01.2 | Chỉnh sửa thông tin tour + toàn bộ POI (replace full POI list) | High |
| FR-01.3 | Soft-delete tour (IsActive = false) | High |
| FR-01.4 | Xem danh sách tours: phân trang (default 10/trang, max 50), tìm kiếm theo tên, lọc trạng thái | High |
| FR-01.5 | Xem chi tiết tour kèm danh sách POI theo StopOrder | High |
| FR-01.6 | Audit trail: CreatedAt, UpdatedAt, CreatedBy (email admin) | High |

### FR-02: Quản lý POI trong Tour

| ID | Mô tả | Priority |
|----|--------|----------|
| FR-02.1 | Thêm POI vào tour: chỉ cho phép chọn POI đang IsActive = true | High |
| FR-02.2 | Xóa POI khỏi tour: chỉ xóa khỏi TourStop, không xóa POI gốc | High |
| FR-02.3 | Sắp xếp thứ tự POI: PUT /api/tour/{id}/reorder, POI set phải khớp chính xác | High |
| FR-02.4 | Giới hạn: tối đa 50 POI/tour | High |
| FR-02.5 | POI đã bị ẩn (IsActive = false) vẫn giữ nguyên trong TourStop, hiển thị "(Đã ẩn)" trên UI | Medium |
| FR-02.6 | Không cho phép POI trùng lặp trong cùng 1 tour (DB unique index trên TourId + PoiPointId) | High |

### FR-03: Trạng thái & Ràng buộc

| ID | Mô tả | Priority |
|----|--------|----------|
| FR-03.1 | Khi xóa Tour: cascade xóa toàn bộ TourStop | High |
| FR-03.2 | Khi xóa POI: restrict nếu có TourStop tham chiếu | High |
| FR-03.3 | Không có endpoint restore tour (re-activate) | Medium |

---

## 7. Non-Functional Requirements

| ID | Mô tả |
|----|--------|
| NFR-01 | **Authentication:** Tất cả endpoints đều yêu cầu JWT Bearer token với role Admin |
| NFR-02 | **Validation:** Tên tour: bắt buộc, 1–200 ký tự. Mô tả: tối đa 1000 ký tự. POI tối đa 50. |
| NFR-03 | **Authorization:** Chỉ role Admin được phép thao tác |
| NFR-04 | **Error handling:** Validation fail → HTTP 400 với `{ "error": "<message>" }`. Not found → HTTP 404. |
| NFR-05 | **Performance:** API danh sách hỗ trợ phân trang + projection query, response < 500ms với 100 tour |
| NFR-06 | **Audit:** Lưu CreatedAt, UpdatedAt (UTC), CreatedBy (email) cho mỗi tour |
| NFR-07 | **Concurrency:** Không có optimistic concurrency token (UpdatedAt không phải row version) — rủi ro lost-update khi nhiều admin sửa cùng lúc |

---

## 8. Data Models

### 8.1 Tour Entity (`TasteVinhKhanh.Shared/Models/Tour.cs`)

```
Tour
├── Id: int                          (PK, auto-increment, identity)
├── Name: string                     (required, max 200)
├── Description: string?            (optional, max 1000)
├── IsActive: bool                   (default true — soft-delete flag)
├── CreatedBy: string                (email admin, max 256)
├── CreatedAt: DateTime              (UTC, auto-set on create)
├── UpdatedAt: DateTime?            (UTC, auto-set on update)
└── TourStops: List<TourStop>       (cascade delete — 1:many)
```

### 8.2 TourStop Entity (`TasteVinhKhanh.Shared/Models/TourStop.cs`)

```
TourStop
├── Id: int                          (PK, auto-increment)
├── TourId: int                      (FK → Tour.Id, required)
├── PoiPointId: int                  (FK → PoiPoint.Id, required, restrict on delete)
├── StopOrder: int                   (1-based position in route)
├── CreatedAt: DateTime              (UTC)
├── UpdatedAt: DateTime?            (UTC)
└── Constraints:
    └── UNIQUE(TourId, PoiPointId)  — không cho phép POI trùng trong tour
    └── INDEX(TourId, StopOrder)
```

### 8.3 Entity Relationship Diagram

```
┌──────────┐       1:M (cascade)        ┌────────────┐
│   Tour   │───────────────────────────▶│  TourStop │
└──────────┘                            └─────┬──────┘
                                               │ M:1 (restrict on delete)
                                               ▼
                                          ┌──────────┐
                                          │ PoiPoint │
                                          └──────────┘
```

> **Lưu ý:** Tour không có FK trực tiếp đến Vendor. Mối quan hệ Tour ↔ Vendor là gián tiếp qua POI.

### 8.4 Response DTOs

**TourListItemDto** — một dòng trong bảng danh sách:
```json
{
  "id": 1,
  "name": "Tour Miền Tây 1 Ngày",
  "description": "Khám phá các điểm ẩm thực nổi tiếng...",
  "poiCount": 5,
  "isActive": true,
  "createdAt": "2026-03-30T10:00:00Z",
  "updatedAt": "2026-03-30T14:30:00Z"
}
```

**TourDetailDto** — chi tiết tour kèm POI:
```json
{
  "id": 1,
  "name": "Tour Miền Tây 1 Ngày",
  "description": "...",
  "isActive": true,
  "createdAt": "2026-03-30T10:00:00Z",
  "updatedAt": "2026-03-30T14:30:00Z",
  "pois": [
    {
      "poiId": 1,
      "poiName": "Chợ nổi Cái Răng",
      "poiIsActive": true,
      "stopOrder": 1
    },
    {
      "poiId": 3,
      "poiName": "Quán A",
      "poiIsActive": false,
      "stopOrder": 2
    }
  ]
}
```

**TourPagedDto** — danh sách phân trang:
```json
{
  "items": [ TourListItemDto ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 10,
  "totalPages": 2
}
```

### 8.5 Request DTOs

**CreateTourRequest** — tạo tour:
```json
{
  "name": "string (required)",
  "description": "string (optional)",
  "poiIds": [ 1, 3, 5 ]  // theo thứ tự muốn sắp xếp
}
```

**UpdateTourRequest** — cập nhật (thay thế toàn bộ POI):
```json
{
  "name": "string (required)",
  "description": "string (optional)",
  "poiIds": [ 1, 2, 3, 4 ]  // thay thế toàn bộ
}
```

**ReorderTourRequest** — chỉ sắp xếp lại:
```json
{
  "poiIds": [ 3, 1, 2 ]  // phải khớp đúng với POIs hiện có trong tour
}
```

---

## 9. API Contracts

> **Base URL:** `/api/tour` (singular)
> **Auth:** `Authorization: Bearer <JWT>` — Role: Admin

### 9.1 GET /api/tour

Lấy danh sách tours (phân trang, tìm kiếm, lọc).

**Query params:**

| Param | Type | Mô tả | Default |
|-------|------|--------|---------|
| page | int | Số trang (≥1) | 1 |
| pageSize | int | Số item/trang (1–50) | 10 |
| search | string | Tìm kiếm theo tên (case-insensitive) | null |
| includeInactive | bool | Bao gồm tours đã xóa | false |

**Response 200:**
```json
{
  "items": [ TourListItemDto ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 10,
  "totalPages": 2
}
```

### 9.2 GET /api/tour/{id}

Lấy chi tiết một tour kèm danh sách POI theo thứ tự.

**Response 200:** `TourDetailDto`
**Response 404:** `{ "error": "Tour không tồn tại." }`

### 9.3 POST /api/tour

Tạo tour mới.

**Request body:** `CreateTourRequest`
**Response 201:** `TourDetailDto` (Location header trỏ đến GET /api/tour/{id})
**Response 400:**
```json
{ "error": "Tên tour không được để trống." }
{ "error": "Tour không được chứa quá 50 điểm." }
{ "error": "POI không hợp lệ hoặc đã bị ẩn: 99, 100" }
```

### 9.4 PUT /api/tour/{id}

Cập nhật tour (thông tin + thay thế toàn bộ POI list).

**Request body:** `UpdateTourRequest`
**Response 200:** `TourDetailDto`
**Response 400:**
```json
{ "error": "Tên tour không được để trống." }
{ "error": "Một hoặc nhiều POI không hợp lệ hoặc đã bị ẩn." }
```
**Response 404:** `{ "error": "Tour không tồn tại." }`

### 9.5 PUT /api/tour/{id}/reorder

Cập nhật thứ tự POI trong tour (chỉ thay đổi StopOrder).

**Request body:** `ReorderTourRequest`
**Response 200:** `TourDetailDto`
**Response 400:**
```json
{ "error": "Danh sách POI không khớp với các điểm hiện có trong tour." }
```
**Response 404:** `{ "error": "Tour không tồn tại." }`

### 9.6 DELETE /api/tour/{id}

Soft-delete tour (IsActive = false).

**Response 204:** No Content
**Response 404:** `{ "error": "Tour không tồn tại." }`

---

## 10. Dependencies & Risks

### Dependencies

| Dep | Mô tả | Ghi chú |
|-----|-------|---------|
| PoiPoint entity | Tour chọn POI từ PoiPoints đang active | Nếu POI bị xóa → restrict (DB) |
| ASP.NET Identity + JWT | Bảo vệ tất cả endpoints | Admin role bắt buộc |
| AppDbContext | Tour + TourStop + PoiPoint configured | Cascade delete Tour→TourStop, Restrict TourStop→PoiPoint |
| Admin Portal (HTML/JS) | Giao diện quản lý tour | Out of scope cho PRD này |

### Risks

| Risk | Mô tả | Mitigation |
|------|-------|------------|
| R01 | POI bị deactivate sau khi đã thêm vào tour → TourStop còn, hiển thị "(Đã ẩn)" | UI hiển thị trạng thái POI (`PoiIsActive`) — giữ nguyên trong tour |
| R02 | Nhiều admin cùng sửa một tour → lost update | **Chưa xử lý** — không có concurrency token. Ghi nhận để xử lý ở v2 |
| R03 | POI bị xóa (hard delete) khi còn tham chiếu TourStop | DB: `OnDelete(DeleteBehavior.Restrict)` — SQL Server reject |
| R04 | Tour không gắn vendor → không có quyền riêng biệt cần xử lý | Rủi ro thấp |

---

## 11. Open Questions

| # | Câu hỏi | Giả định |
|---|---------|----------|
| OQ-01 | MauiApp có cần sync tours không? | **Không.** MVP: MauiApp chỉ sync POIs. Tours quản lý riêng trên Admin. |
| OQ-02 | Admin có cần duplicate/copy tour không? | **Không.** MVP loại trừ. |
| OQ-03 | Tour có hỗ trợ đa ngôn ngữ (VI/EN) không? | **Không.** Tên/mô tả tour chỉ có 1 ngôn ngữ (VI). |
| OQ-04 | Có giới hạn số tour được tạo không? | **Không.** Không giới hạn. |
| OQ-05 | POI inactive có hiển thị trong tour detail không? | **Có.** Hiển thị với trạng thái `(Đã ẩn)` trên UI. TourStop không bị xóa. |
| OQ-06 | Sort order mặc định khi tạo tour? | Theo thứ tự POI được truyền trong `poiIds` (index → StopOrder = i+1). |
| OQ-07 | Có cần endpoint restore (re-activate) tour đã xóa không? | **Không.** MVP loại trừ. Soft-delete vĩnh viễn. |
| OQ-08 | Optimistic concurrency — khi nào xử lý? | **V2.** Hiện tại chưa xử lý (R02). |

---

## 12. Future Enhancements (Post-MVP)

- Giao diện bản đồ trên admin để visualize tour route
- Tối ưu lộ trình tự động (TSP solver)
- Lịch trình tour (thời gian bắt đầu/kết thúc dự kiến)
- Xuất tour cho người dùng cuối (giao diện mobile)
- Duplicate/copy tour
- Tour đa ngôn ngữ
- Bulk actions (activate/deactivate nhiều tour)
- Optimistic concurrency cho multi-admin editing (R02)
- Endpoint restore/re-activate tour đã xóa

---

## 13. DoD (Definition of Done)

Trước khi coi là hoàn thành, tất cả các điều sau phải đạt:

### Backend API
- [ ] `GET /api/tour` — phân trang, tìm kiếm, lọc includeInactive đúng
- [ ] `GET /api/tour/{id}` — trả đúng TourDetailDto với Pois theo StopOrder
- [ ] `POST /api/tour` — tạo Tour + TourStops, CreatedAt/CreatedBy đúng
- [ ] `PUT /api/tour/{id}` — replace toàn bộ POI list, UpdatedAt cập nhật
- [ ] `PUT /api/tour/{id}/reorder` — cập nhật StopOrder đúng, validate POI set khớp
- [ ] `DELETE /api/tour/{id}` — soft-delete (IsActive = false), HTTP 204
- [ ] Validation: tên rỗng → 400, tên > 200 → 400, mô tả > 1000 → 400, POI > 50 → 400
- [ ] Validation: POI không tồn tại hoặc inactive → 400
- [ ] Reorder: POI set không khớp → 400
- [ ] JWT auth bảo vệ tất cả endpoints (HTTP 401/403 nếu không có token hợp lệ)
- [ ] `dotnet build` thành công, không warnings

### Database
- [ ] Bảng `Tours` tồn tại với đúng schema
- [ ] Bảng `TourStops` tồn tại với unique index (TourId, PoiPointId)
- [ ] FK `TourStops.PoiPointId → PoiPoints.Id` có OnDelete(Restrict)

### Service Layer
- [ ] `ITourService` implemented đúng, tất cả method hoạt động
- [ ] Projection query cho danh sách (không load navigation khối lượng)
- [ ] Unit test (nếu có) cho các validation edge cases

---

## 14. SQL Schema (SSMS — tạo thủ công)

> Các bảng này được tạo thủ công trong SQL Server (không qua EF migrations). Chạy script bên dưới trong SSMS trước khi chạy API.

### 14.1 Bảng Tours
```sql
CREATE TABLE Tours (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    Name             NVARCHAR(200)  NOT NULL,
    Description      NVARCHAR(1000) NULL,
    IsActive         BIT           NOT NULL DEFAULT(1),
    CreatedBy        NVARCHAR(256) NOT NULL,
    CreatedAt        DATETIME2     NOT NULL DEFAULT(GETUTCDATE()),
    UpdatedAt         DATETIME2     NULL
);
```

### 14.2 Bảng TourStops
```sql
CREATE TABLE TourStops (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    TourId        INT NOT NULL,
    PoiPointId    INT NOT NULL,
    StopOrder     INT NOT NULL,
    CreatedAt     DATETIME2  NOT NULL DEFAULT(GETUTCDATE()),
    UpdatedAt     DATETIME2  NULL,

    CONSTRAINT FK_TourStops_Tour      FOREIGN KEY (TourId)     REFERENCES Tours(Id)      ON DELETE CASCADE,
    CONSTRAINT FK_TourStops_PoiPoint FOREIGN KEY (PoiPointId) REFERENCES PoiPoints(Id) ON DELETE RESTRICT,
    CONSTRAINT UQ_TourStops_Tour_Poi  UNIQUE (TourId, PoiPointId)
);

CREATE INDEX IX_TourStops_Tour_Order ON TourStops (TourId, StopOrder);
```

---

## 15. Seed Data (ví dụ)

> Dữ liệu mẫu để test — chạy sau khi tạo bảng.

```sql
-- Tour mẫu 1 (3 POI, active)
INSERT INTO Tours (Name, Description, IsActive, CreatedBy, CreatedAt)
VALUES (N'Tour Ẩm thực Vĩnh Khánh 1', N'Tuyến ẩm thực dọc phố Vĩnh Khánh, quận 4, TP.HCM', 1, 'admin@vinhkhanh.com', GETUTCDATE());

INSERT INTO TourStops (TourId, PoiPointId, StopOrder, CreatedAt)
VALUES
    (1, 1, 1, GETUTCDATE()),
    (1, 2, 2, GETUTCDATE()),
    (1, 3, 3, GETUTCDATE());

-- Tour mẫu 2 (2 POI, active)
INSERT INTO Tours (Name, Description, IsActive, CreatedBy, CreatedAt)
VALUES (N'Tour Ẩm thực Vĩnh Khánh 2', N'Tuyến ẩm thực ngắn gồm 2 quán nổi bật', 1, 'admin@vinhkhanh.com', GETUTCDATE());

INSERT INTO TourStops (TourId, PoiPointId, StopOrder, CreatedAt)
VALUES
    (2, 4, 1, GETUTCDATE()),
    (2, 5, 2, GETUTCDATE());

-- Tour mẫu 3 (đã xóa)
INSERT INTO Tours (Name, Description, IsActive, CreatedBy, CreatedAt)
VALUES (N'Tour đã xóa', N'Ví dụ tour inactive', 0, 'admin@vinhkhanh.com', GETUTCDATE());
```

---

## 16. Frontend UI Specifications

> Mô tả chi tiết trạng thái giao diện cho Admin Portal (tour.html). Giúp dev frontend biết cần xử lý gì.

### 16.1 Trang danh sách Tours

| Trạng thái | Hành vi |
|-----------|---------|
| **Loading** | Hiển thị skeleton table hoặc spinner ở vùng bảng, vô hiệu hóa nút phân trang |
| **Data loaded** | Bảng hiển thị đầy đủ cột, pagination ở dưới, search bar ở trên |
| **Empty state** | Hiển thị icon/trống kèm text: "Chưa có tour nào. Nhấn 'Tạo Tour' để bắt đầu." |
| **Error** | Toast/alert màu đỏ hiển thị message lỗi, giữ nguyên dữ liệu cũ |
| **Search active** | Bảng cập nhật theo kết quả tìm kiếm, label "Tìm thấy N tour" |
| **Filter includeInactive** | Checkbox/toggle hiện cả tour đã xóa, bảng thêm cột trạng thái rõ ràng |

### 16.2 Modal tạo / chỉnh sửa Tour

| Trạng thái | Hành vi |
|-----------|---------|
| **Initial load** | Fetch danh sách POI đang active (`GET /api/poi?isActive=true`), hiển thị dropdown/list |
| **Name empty** | Nút "Lưu" bị disabled, hiện validation message dưới input |
| **Submitting** | Disable nút "Lưu", hiện spinner, không cho đóng modal |
| **Success** | Toast "Tạo tour thành công" / "Cập nhật tour thành công", đóng modal, refresh bảng |
| **Validation error** | Hiện inline error dưới field lỗi, không đóng modal |
| **POI list full (50)** | Disable input thêm POI, tooltip: "Đã đạt giới hạn 50 POI/tour" |

### 16.3 Modal chi tiết Tour

| Trạng thái | Hành vi |
|-----------|---------|
| **Loading** | Skeleton/spinner ở vùng POI list |
| **POI active** | Icon/check màu xanh, tên POI bình thường |
| **POI inactive** | Tên POI + badge đỏ "(Đã ẩn)", không cho thao tác |
| **Reorder in progress** | Nút ↑↓ disabled nếu là POI đầu tiên (↑) hoặc cuối cùng (↓) |

### 16.4 Toast Notifications

| Trigger | Message | Loại |
|---------|---------|------|
| Tạo tour thành công | "Tour đã được tạo thành công." | success |
| Cập nhật tour thành công | "Tour đã được cập nhật thành công." | success |
| Sắp xếp thành công | "Đã cập nhật thứ tự các điểm trong tour." | success |
| Xóa tour thành công | "Tour đã được xóa." | success |
| Validation fail | `error` từ API (400) | error |
| Network error | "Không thể kết nối server. Vui lòng thử lại." | error |
| Confirm dialog cancel | Không hiển thị toast — hủy thao tác | — |

---

## 17. Implementation Checklist

Theo quy trình `CLAUDE.md`:

- [ ] Thêm DTOs trong `TasteVinhKhanh.Shared/DTOs/Dtos.cs` (đã có)
- [ ] Thêm entity `Tour`, `TourStop` trong `TasteVinhKhanh.Shared/Models/` (đã có)
- [ ] Cấu hình DbContext trong `AppDbContext` (đã có)
- [ ] Thêm `ITourService` + `TourService` trong `TasteVinhKhanh.Api/Services/`
- [ ] Đăng ký DI trong `Program.cs`
- [ ] Thêm `TourController` trong `TasteVinhKhanh.Api/Controllers/`
- [ ] Tạo bảng `Tours`, `TourStops` trong SQL Server (SSMS)
- [ ] Cập nhật `CLAUDE.md` nếu có thay đổi kiến trúc
