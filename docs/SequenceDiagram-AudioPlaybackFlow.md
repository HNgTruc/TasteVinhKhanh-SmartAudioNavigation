# Audio Playback Flow — Sequence Diagram

> Mô tả luồng từ khi ứng dụng MauiApp khởi động → phát audio thuyết minh khi user đến gần POI → sync log lên server.

---

## 1. Tổng quan luồng chính

```
┌──────────────┐     ┌──────────────┐     ┌───────────────────────┐     ┌────────────┐
│  MauiApp     │     │  Geofence    │     │  NarrationEngine      │     │  Server    │
│  (User)      │     │  Engine      │     │  (Audio Player)       │     │  (API)     │
└──────┬───────┘     └──────┬───────┘     └───────────┬───────────┘     └──────┬─────┘
       │                    │                         │                        │
       │ [App Launch]       │                         │                        │
       │───────────────────>│                         │                        │
       │                    │ Init (load POIs from    │                        │
       │                    │  local SQLite)          │                        │
       │                    │                         │                        │
       │ [GPS: every 5s]    │                         │                        │
       │───────────────────>│                         │                        │
       │                    │ CheckLocationAsync(loc) │                        │
       │                    │                         │                        │
       │                    │──HaversineMeters()──>   │                        │
       │                    │                         │                        │
       │                    │ [distance <= TriggerRadius]                      │
       │                    │                         │                        │
       │                    │ WasRecentlyPlayedAsync()                         │
       │                    │                         │                        │
       │                    │ [5-min cooldown OK]     │                        │
       │                    │                         │                        │
       │                    │──PoiTriggered event──>  │                        │
       │                    │                         │                        │
       │                    │                         │ ShowPoiNotification()  │
       │                    │                         │ PlayAsync(poi,dist,loc)│
       │                    │                         │                        │
       │                    │                         │ InsertLogAsync() ─────>│ POST /analytics/logs
       │                    │                         │                        │
       │                    │                         │ [IsAudioDownloaded ?]  │
       │                    │                         │                        │
       │                    │                         │ [YES → play local]     │
       │                    │                         │ PlayLocalFile()        │
       │                    │                         │                        │
       │                    │                         │ [NO → download first]  │
       │                    │                         │ DownloadAudioAsync()──>│ GET /api/audio/{id}
       │                    │                         │                        │
       │                    │                         │ PlayLocalFile()        │
       │                    │                         │                        │
       │                    │                         │ [download fail → TTS]  │
       │                    │                         │ SpeakWithTtsAsync()    │
       │                    │                         │ (TextToSpeech)         │
       │                    │                         │                        │
       │                    │                         │ NarrationFinished event│
       │                    │                         │                        │
       │                    │                         │UploadPendingLogsAsync()──>│
```

---

## 2. Chi tiết từng phase

### Phase 1: App Launch & Initial Sync

```
Actor: MauiApp
Participant: SyncController (API) | SyncService (MAUI) | AppDatabase (SQLite)

MauiApp              SyncService(MAUI)        AppDatabase           SyncController(API)
   │                       │                       │                       │
   │ InitAsync()           │                       │                       │
   │──────────────────────>│                       │                       │
   │                       │                       │                       │
   │                       │ [First launch?]       │                       │
   │                       │                       │                       │
   │                       │ GetLastSyncTime()     │                       │
   │                       │──────────────────────>│                       │
   │                       │                       │                       │
   │                       │                       │ Return lastSyncAt     │
   │                       │<──────────────────────│                       │
   │                       │                       │                       │
   │                       │ SyncPoisAsync(lastSyncAt)                     │
   │                       │──────────────────────────────────────────────>│
   │                       │                       │                       │
   │                       │                       │        GET /api/sync  │
   │                       │                       │        ?lastSyncAt=...│
   │                       │                       │                       │
   │                       │<──────────────────────────────────────────────│
   │                       │         200 OK: {Pois[], SyncedAt}            │
   │                       │                       │                       │
   │                       │                       │                       │
   │                       │ UpsertPoisFromServer(Pois)                    │
   │                       │──────────────────────>│                       │
   │                       │                       │                       │
   │                       │                       │ Insert/Update POIs    │
   │                       │                       │ Insert/Update AudioScripts
   │                       │                       │                       │
   │                       │ SetLastSyncTime(SyncedAt)                     │
   │                       │──────────────────────>│                       │
   │                       │                       │                       │
   │ [App ready — GPS starts]                      │                       │
   │                       │                       │                       │
```

### Phase 2: Location Detection & Geofence Check

```
Actor: MauiApp (GPS)
Participant: LocationService | GeofenceEngine | AppDatabase

LocationService      GeofenceEngine          AppDatabase
      │                   │                     │
      │ [GPS poll 5s]     │                     │
      │ StartAsync()      │                     │
      │                   │                     │
      │ OnLocationUpdated(loc)                  │
      │──────────────────>│                     │
      │                   │                     │
      │                   │ CheckLocationAsync(loc)
      │                   │                     │
      │                   │ GetAllPoisAsync()   │
      │                   │────────────────────>│
      │                   │                     │
      │                   │<────────────────────│
      │                   │ List<LocalPoi>      │
      │                   │                     │
      │                   │ [for each POI]      │
      │                   │──HaversineMeters()─>│
      │                   │ distance = X meters │
      │                   │<────────────────────│
      │                   │                     │
      │                   │ [distance <= poi.TriggerRadius]
      │                   │                     │
      │                   │ WasRecentlyPlayedAsync(poiId, 5min)
      │                   │────────────────────>│
      │                   │                     │
      │                   │<────────────────────│
      │                   │ true/false (cooldown)
      │                   │                     │
      │                   │ [cooldown active → skip]
      │                   │                     │
      │                   │ [cooldown OK]       │
      │                   │                     │
      │                   │──PoiTriggered event │
      │                   │ (poi, distance, location)
      │                   │                     │
```

### Phase 3: Audio Playback (3-Tier Fallback)

```
Actor: GeofenceEngine
Participant: NarrationEngine | AudioPlayerService | AppDatabase | AudioController (API) | TTS

GeofenceEngine       NarrationEngine          AppDatabase        AudioPlayerService    AudioController(API)   TTS
     │                     │                     │                    │                     │                   │
     │                     │ PlayAsync(poi)      │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ InsertLogAsync()──────>                  │                     │                   │
     │                     │                     │ Insert playback log (IsSynced=false)     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ GetAudioScript(poiId, lang)              │                     │                   │
     │                     │───────────────────>│                     │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │<────────────────────│                    │                     │                   │
     │                     │ LocalAudioScript    │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ [IsAudioDownloaded && local file exists?]│                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ [YES: Tier 1]       │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ PlayLocalFile(localPath)                 │                     │                   │
     │                     │─────────────────────────────────────────>│                     │                   │
     │                     │                     │                    │ Read bytes          │                   │
     │                     │                     │                    │ Play                │                   │
     │                     │<─────────────────────────────────────────│                     │                   │
     │                     │ PlaybackEnded       │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ [NO: Tier 2]        │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ DownloadAudioAsync(scriptId)             │                     │                   │
     │                     │───────────────────────────────────────────────────────────────>│                   │
     │                     │                     │                    │                     │ GET /api/audio/{id}
     │                     │                     │                    │                     │                   │
     │                     │                     │                    │<────────────────────────────────────────│
     │                     │                     │                    │ 200 OK: audio bytes │                   │
     │                     │                     │                    │                     │                   │
     │                     │ Save to cache       │                    │                     │                   │
     │                     │────────────────────>│                    │                     │                   │
     │                     │                     │ UpdateLocalAudioPath()                   │                   │
     │                     │                     │                    │                     │                   │
     │                     │ PlayLocalFile()     │                    │                     │                   │
     │                     │─────────────────────────────────────────>│                     │                   │
     │                     │                     │                    │ Play                │                   │
     │                     │<─────────────────────────────────────────│                     │                   │
     │                     │ PlaybackEnded       │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ [Tier 2 failed: Tier 3]                  │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ SpeakWithTtsAsync(ttsScript, lang)       │                     │                   │
     │                     │───────────────────────────────────────────────────────────────────────────────────>│
     │                     │                     │                    │                     │                   │
     │                     │                     │                    │                     │                   │
     │                     │ NarrationFinished   │                    │                     │                   │
     │                     │                     │                    │                     │                   │
```

### Phase 4: Playback Log Sync

```
Actor: (Background after playback)
Participant: SyncService (MAUI) | AppDatabase | AnalyticsController (API)

SyncService(MAUI)      AppDatabase           AnalyticsController(API)
       │                    │                     │
       │                    │                     │
       │ UploadPendingLogsAsync()                 │
       │                    │                     │
       │ GetUnsyncedLogsAsync()                   │
       │───────────────────>│                     │
       │                    │                     │
       │<───────────────────│                     │
       │ List<LocalPlaybackLog>                   │
       │                    │                     │
       │ [batch upload]     │                     │
       │ POST /api/analytics/logs                 │
       │─────────────────────────────────────────>│
       │                    │                     │
       │                    │     SaveLogsAsync() │
       │                    │    → PlaybackLogs DB│
       │                    │                     │
       │<─────────────────────────────────────────│
       │         200 OK     │                     │
       │                    │                     │
       │ MarkLogsSyncedAsync(logs)                │
       │───────────────────>│                     │
       │                    │ Update IsSynced=true│
       │                    │                     │
```

---

## 3. Sequence Diagram tổng hợp (single view)

```
┌──────────────┐   ┌────────────────┐  ┌──────────────────┐  ┌────────────┐  ┌──────────────────┐  ┌─────────────┐
│   User/App   │   │ LocationService│  │  GeofenceEngine  │  │ AppDatabase│  │ NarrationEngine  │  │  Server API │
└───────┬──────┘   └───────┬────────┘  └────────┬─────────┘  └─────┬──────┘  └────────┬─────────┘  └──────┬──────┘
        │                  │                    │                  │                  │                   │
        │ App Launch       │                    │                  │                  │                   │
        │─────────────────>│                    │                  │                  │                   │
        │                  │ StartAsync()       │                  │                  │                   │
        │                  │───────────────────>│                  │                  │                   │
        │                  │                    │ Load POIs from   │                  │                   │
        │                  │                    │────────────────> │                  │                   │
        │                  │                    │                  │                  │                   │
        │ [GPS 5s poll]    │                    │                  │                  │                   │
        │<─────────────────│                    │                  │                  │                   │
        │                  │                    │                  │                  │                   │
        │ LocationUpdated  │                    │                  │                  │                   │
        │─────────────────>│                    │                  │                  │                   │
        │                  │ OnLocationUpdated  │                  │                  │                   │
        │                  │──────────────────> │                  │                  │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │ CheckLocationAsync(lat/lng)         │                   │
        │                  │                    │HaversineMeters()─>│                 │                   │
        │                  │                    │<──distance(m)────│                  │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │ WasRecentlyPlayedAsync(poiId, 5min) │                   │
        │                  │                    │────────────────> │                  │                   │
        │                  │                    │<──────────────── │                  │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │ [cooldown OK]    │                  │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │──PoiTriggered──> │                  │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ ShowNotification()                   │
        │                  │                    │                  │─────────────────>│                   │
        │                  │                    │                  │                  │ Notify user       │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │PlayAsync(poi, dist, loc)             │
        │                  │                    │                  │<──────────────── │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ InsertLogAsync() │                   │
        │                  │                    │                  │─────────────────>│                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ GetAudioScript() │                   │
        │                  │                    │                  │─────────────────>│                   │
        │                  │                    │                  │<─────────────────│                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ IsAudioDownloaded?                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ [YES] PlayLocalFile()                │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ [NO] DownloadAudioAsync(scriptId)    │
        │                  │                    │                  │─────────────────────────────────────>│
        │                  │                    │                  │                  │ GET /api/audio/{id}
        │                  │                    │                  │<─────────────────────────────────────│
        │                  │                    │                  │                  │ 200 OK (audio bytes)
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ SaveCache(localPath)                 │
        │                  │                    │                  │────────────────> │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ PlayLocalFile()  │                   │
        │                  │                    │                  │──────────────────│                   │
        │                  │                    │                  │                  │ Audio plays       │
        │                  │                    │                  │<──────────────── │                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ NarrationFinished│                   │
        │                  │                    │                  │                  │                   │
        │                  │                    │                  │ UploadPendingLogsAsync()             │
        │                  │                    │                  │────────────────> │                   │
        │                  │                    │                  │                  │ POST /api/analytics/logs
        │                  │                    │                  │<─────────────────────────────────────│
        │                  │                    │                  │                   │ 200 OK           │
        │                  │                    │                  │ MarkLogsSynced()  │                  │
        │                  │                    │                  │──────────────────>│                  │
        │                  │                    │                  │                   │                  │
```

---

## 4. Thành phần & File mapping

| Thành phần                 | File trong codebase                                                    |
| -------------------------- | ---------------------------------------------------------------------- |
| Location polling           | `MauiApp/Services/LocationService.cs`                                  |
| Geofence check             | `MauiApp/Services/GeofenceEngine.cs`                                   |
| Audio orchestration        | `MauiApp/Services/NarrationEngine.cs`                                  |
| Audio download + playback  | `MauiApp/Services/AudioPlayerService.cs`                               |
| Local SQLite (POIs + logs) | `MauiApp/Data/AppDatabase.cs`                                          |
| Sync với server            | `MauiApp/Services/SyncService.cs`                                      |
| Push notification          | `MauiApp/Services/NotificationService.cs`                              |
| API: sync POIs             | `Api/Controllers/SyncController.cs` → `GET /api/sync`                  |
| API: serve audio           | `Api/Controllers/AudioController.cs` → `GET /api/audio/{id}`           |
| API: device register       | `Api/Controllers/AuthController.cs` → `POST /api/auth/device-register` |
| API: analytics logs        | `Api/Controllers/AnalyticsController.cs` → `POST /api/analytics/logs`  |

---

---

# Image Upload & Approval Flow — Sequence Diagram

> Mô tả luồng Vendor upload ảnh lên staging → Admin duyệt hoặc từ chối → ảnh hiển thị trên app. Gồm 2 nhánh: Upload path và Deletion path.

---

## 1. Tổng quan — Image State Machine

```
┌──────────────┐   vendor uploads    ┌─────────────┐  admin approves   ┌──────────┐
│  (not exist) │ ─────────────────>  │   Staging   │───────────────>   │ Approved │
└──────────────┘                     │  (Pending)  │                   │ (visible)│
                                     └─────────────┘                   └──────────┘
                                            │
                                            │ admin rejects
                                            ↓
                                      ┌──────────┐
                                      │ Rejected │ ──> file deleted from disk
                                      └──────────┘

── Deletion path ──────────────────────────────────────────────────

┌──────────┐  vendor requests   ┌───────────────┐  admin approves  ┌────────┐
│ Approved │ ────────────────>  │ DeletionQueue │ ──────────────>  │Deleted │
└──────────┘                    │   (Pending)   │                  └────────┘
                                └───────────────┘
                                         │
                                         │ admin rejects
                                         ↓
                                   ┌──────────┐
                                   │ Rejected │ (image kept)
                                   └──────────┘
```

---

## 2. Path 1: Vendor Upload → Admin Approval

```
Actor: Vendor (web portal) | Admin (dashboard)
Participant: VendorController (API) | AdminVendorController (API) | AppDbContext | FileSystem (wwwroot)

Vendor           VendorController         AppDbContext          Admin            AdminVendorController    FileSystem
   │                    │                     │                  │                      │                   │
   │ [Select image]     │                     │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │ POST /api/vendor/images/staging          │                  │                      │                   │
   │ {poiId, file}      │                     │                  │                      │                   │
   │───────────────────>│                     │                  │                      │                   │
   │                    │ ValidateVendorPoi(poiId, vendorId)     │                      │                   │
   │                    │────────────────────>│                  │                      │                   │
   │                    │<────────────────────│                  │                      │                   │
   │                    │ [not owner → 403]   │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │ [Save to staging]   │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │ SaveFileAsync(file, staging/poi_{poiId}/{guid}.{ext})         │                   │
   │                    │──────────────────────────────────────────────────────────────>│                   │
   │                    │                     │                  │                      │                   │
   │                    │<──────────────────────────────────────────────────────────────│                   │
   │                    │      file path      │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │ AddStagingImage(StagingType=Upload, Status=Pending, ...)      │                   │
   │                    │────────────────────>│                  │                      │                   │
   │                    │                     │ Insert StagingImage record              │                   │
   │                    │<────────────────────│                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │<────────────────── │ 200: {stagingId, tempUrl}              │                      │                   │
   │ [Upload success — "Pending approval"]    │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │         GET /api/admin/staging-images   │                   │
   │                    │                     │                  │<─────────────────────│                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │ List pending upload  │                   │
   │                    │                     │<─────────────────│                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │         200: [StagingImage, ...]        │                   │
   │                    │                     │                  │─────────────────────>│                   │
   │                    │                     │                  │ [Admin sees pending] │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ [Admin clicks "Approve"]
   │                    │                     │                  │                      │─────────────────> │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ POST /api/admin/staging-images/{id}/approve
   │                    │                     │                  │                      │<───────────────── │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ GetStagingImage(id)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │ stagingImg        │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ CopyFile(staging/..., images/poi_{poiId}/)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ DeleteFile(staging/poi_{poiId}/...) │
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ AddRestaurantImage(PoiPointId, ImageUrl, IsPrimary, SortOrder)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ UpdateStagingImage(Status=Approved)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ [First approved image?]
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ SetPoiPoint.ImageUrl(primaryUrl)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │         200 OK       │                   │
   │                    │                     │                  │<─────────────────────│                   │
   │                    │                     │                  │ [Image visible on app]                   │
   │                    │                     │                  │                      │                   │
```

### 2b. Admin Reject

```
Vendor           VendorController         AdminVendorController    FileSystem
   │                    │                      │                   │
   │                    │                      │ POST /api/admin/staging-images/{id}/reject
   │                    │                      │<───────────────── │
   │                    │                      │                   │
   │                    │                      │ GetStagingImage(id)
   │                    │                      │──────────────────>│
   │                    │                      │<──────────────────│
   │                    │                      │                   │
   │                    │                      │ DeleteFile(staging/poi_{poiId}/{filename})
   │                    │                      │─────────────────────────────────────>│
   │                    │                      │                   │
   │                    │                      │ UpdateStagingImage(Status=Rejected)
   │                    │                      │──────────────────>│
   │                    │                      │<──────────────────│
   │                    │                      │                   │
   │                    │                      │         200 OK    │
   │                    │                      │──────────────────>│
   │                    │                      │                   │
```

---

## 3. Path 2: Vendor Request Deletion → Admin Approval

```
Actor: Vendor | Admin
Participant: VendorController | AdminVendorController | AppDbContext | FileSystem

Vendor           VendorController         AppDbContext          Admin            AdminVendorController    FileSystem
   │                    │                     │                  │                      │                   │
   │ POST /api/vendor/images/delete-request   │                  │                      │                   │
   │ {imageId, poiPointId}                    │                  │                      │                   │
   │───────────────────>│                     │                  │                      │                   │
   │                    │ ValidateVendorOwnsImage(imageId, vendorId)│                   │                   │
   │                    │────────────────────>│                  │                      │                   │
   │                    │<────────────────────│                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │ AddStagingImage(    │                  │                      │                   │
   │                    │   StagingType=Deletion,                │                      │                   │
   │                    │   Status=Pending,                      │                      │                   │
   │                    │   OriginalImageId=imageId              │                      │                   │
   │                    │ )                                      │                      │                   │
   │                    │────────────────────>│                  │                      │                   │
   │                    │                     │ Insert StagingImage record              │                   │
   │                    │<────────────────────│                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │<───────────────────│ 200: {stagingId, requestId}            │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │         GET /api/admin/staging-images/deletion              │
   │                    │                     │                  │<─────────────────────│                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │List Deletion requests│                   │
   │                    │                     │<─────────────────│                      │                   │
   │                    │                     │                  │                      │                   │
   │                    │                     │         200: [StagingImage, ...]        │                   │
   │                    │                     │                  │─────────────────────>│                   │
   │                    │                     │                  │ [Admin sees deletion request]            │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ [Admin clicks "Approve"]
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ POST /api/admin/staging-images/{id}/approve-deletion
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ GetStagingImage(id)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │ stagingImg (Deletion)
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ GetRestaurantImage(originalImageId)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │ restaurantImage   │
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ DeleteFile(images/poi_{poiId}/{filename})
   │                    │                     │                  │                      │─────────────────────────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ DeleteRestaurantImage(originalImageId)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │ UpdateStagingImage(Status=Approved)
   │                    │                     │                  │                      │──────────────────>│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │                      │<──────────────────│
   │                    │                     │                  │                      │                   │
   │                    │                     │                  │         200 OK       │                   │
   │                    │                     │                  │<─────────────────────│                   │
   │                    │                     │                  │ [Image removed from app]│                │
   │                    │                     │                  │                      │                   │
```

### 3b. Admin Reject Deletion (keep image)

```
Admin            AdminVendorController    AppDbContext
  │                      │                   │
  │ POST /api/admin/staging-images/{id}/reject-deletion
  │<─────────────────────│                   │
  │                      │                   │
  │                      │ UpdateStagingImage(Status=Rejected)
  │                      │──────────────────>│
  │                      │<──────────────────│
  │                      │                   │
  │         200 OK       │                   │
  │─────────────────────>│                   │
  │ [Image stays visible on app]             │
```

---

## 4. Tổng hợp — Full Vendor Upload Flow (single view)

```
┌──────────┐   ┌───────────────────┐  ┌─────────────┐  ┌──────────┐  ┌─────────────────────┐  ┌──────────┐  ┌──────────┐
│  Vendor  │   │  VendorController │  │AppDbContext │  │  Admin   │  │AdminVendorController│  │FileSystem│  │ PoiPoint │
└────┬─────┘   └────────┬──────────┘  └──────┬──────┘  └────┬─────┘  └──────────┬──────────┘  └────┬─────┘  └────┬─────┘
     │                  │                    │              │                   │                  │             │
     │ POST /api/vendor/images/staging       │              │                   │                  │             │
     │ {poiId, file}   │                     │              │                   │                  │             │
     │────────────────>│                     │              │                   │                  │             │
     │                  │ ValidateOwner      │              │                   │                  │             │
     │                  │───────────────────>│              │                   │                  │             │
     │                  │<───────────────────│              │                   │                  │             │
     │                  │                    │              │                   │                  │             │
     │                  │ SaveFile(staging/poi_{id}/{guid}.{ext})               │                  │             │
     │                  │──────────────────────────────────────────────────────>│                  │             │
     │                  │<──────────────────────────────────────────────────────│                  │             │
     │                  │   filePath         │              │                   │                  │             │
     │                  │                    │              │                   │                  │             │
     │                  │ AddStagingImage(Upload, Pending)  │                   │                  │             │
     │                  │───────────────────>│              │                   │                  │             │
     │                  │                    │ Insert       │                   │                  │             │
     │                  │<───────────────────│              │                   │                  │             │
     │                  │                    │              │                   │                  │             │
     │ 200: stagingId   │                    │              │                   │                  │             │
     │<──────────────── │                    │              │                   │                  │             │
     │                  │                    │              │                   │                  │             │
     │                  │                    │    GET /api/admin/staging-images │                  │             │
     │                  │                    │              │<───────────────────│                 │             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │ List pending       │                 │             │
     │                  │                    │<─────────────│                    │                 │             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │   200: [items]                   │                  │             │
     │                  │                    │              │───────────────────>│                 │             │
     │                  │                    │              │ [Admin reviews]    │                 │             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ [Approve click] │             │
     │                  │                    │              │                    │────────────────>│             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ POST /api/admin/staging-images/{id}/approve
     │                  │                    │              │                    │<────────────────│             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ GetStagingImage │             │
     │                  │                    │              │                    │────────────────>│             │
     │                  │                    │              │                    │<────────────────│             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ CopyFile(staging → images/poi_{id}/)
     │                  │                    │              │                    │──────────────────────────────>│
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ DeleteFile(staging/)│         │
     │                  │                    │              │                    │──────────────────────────────>│
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ AddRestaurantImage()          │
     │                  │                    │              │                    │────────────────>│             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │<────────────────│             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ UpdateStatus(Approved)        │
     │                  │                    │              │                    │────────────────>│             │
     │                  │                    │              │                    │<────────────────│             │
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │ [First image?]  │             │
     │                  │                    │              │                    │ SetImageUrl(primaryUrl)       │
     │                  │                    │              │                    │──────────────────────────────>│
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │                    │<──────────────────────────────│
     │                  │                    │              │                    │                 │             │
     │                  │                    │              │         200 OK     │                 │             │
     │                  │                    │              │<───────────────────│                 │             │
     │                  │                    │              │ [Image live on app]│                 │             │
```

---

## 5. Thành phần & File mapping

| Thành phần                   | File trong codebase                                                                                 |
| ---------------------------- | --------------------------------------------------------------------------------------------------- |
| Vendor upload (staging)      | `Api/Controllers/VendorController.cs` → `POST /api/vendor/images/staging`                           |
| Vendor delete request        | `Api/Controllers/VendorController.cs` → `POST /api/vendor/images/delete-request`                    |
| Admin list staging           | `Api/Controllers/AdminVendorController.cs` → `GET /api/admin/staging-images`                        |
| Admin list deletion requests | `Api/Controllers/AdminVendorController.cs` → `GET /api/admin/staging-images/deletion`               |
| Admin approve upload         | `Api/Controllers/AdminVendorController.cs` → `POST /api/admin/staging-images/{id}/approve`          |
| Admin reject upload          | `Api/Controllers/AdminVendorController.cs` → `POST /api/admin/staging-images/{id}/reject`           |
| Admin approve deletion       | `Api/Controllers/AdminVendorController.cs` → `POST /api/admin/staging-images/{id}/approve-deletion` |
| Admin reject deletion        | `Api/Controllers/AdminVendorController.cs` → `POST /api/admin/staging-images/{id}/reject-deletion`  |
| Staging entity               | `Shared/Models/StagingImage.cs`                                                                     |
| Staging DB                   | `Api/Data/AppDbContext.cs`                                                                          |
| File storage                 | `wwwroot/staging/` + `wwwroot/images/`                                                              |

---

## 6. Ghi chú

- **2-step upload**: Ảnh vendor luôn qua staging trước → Admin duyệt → đảm bảo chất lượng hiển thị trên app
- **StagingType**: `Upload` | `Deletion` | `Logo` | `LogoDeletion` — dùng để phân biệt loại thao tác
- **Primary image**: Ảnh đầu tiên được approve sẽ tự động gán làm `PoiPoint.ImageUrl`
- **File path convention**: `staging/poi_{poiId}/images/{guid}.{ext}` → sau approve: `images/poi_{poiId}/{guid}.{ext}`
- **Deletion**: KHÔNG xóa file ngay khi vendor request — chỉ xóa khi Admin approve, giữ lại nếu Admin reject

---

---

# Authentication Flow — Sequence Diagram

> Mô tả luồng đăng nhập → nhận JWT → truy cập API có role-based authorization. Gồm 4 nhánh: Admin Login, Vendor Login (endpoint riêng), Vendor Registration/Forgot Password, Device Registration (MAUI).

---

## 1. Tổng quan — Authentication Architecture

```
┌─────────┐   POST /api/auth/login hoặc /api/auth/vendor-login   ┌─────────────────┐   Validate   ┌──────────────┐
│ Browser │ ──────────────────────────> │  AuthController │ ──────────>  │  UserManager │
│  (Admin/│   {email, password}         │                 │  FindByEmail │  (ASP.NET    │
│  Vendor)│                             │                 │  + CheckPwd  │   Identity)  │
└────┬────┘                             └────────┬────────┘              └───────┬──────┘
     │                                           │                               │
     │                                           │        Claims: NameId, Email, │
     │                                           │        Role, (VendorId)       │
     │                                           │<──────────────────────────────│
     │                                           │                               │
     │   200: { accessToken, expiresAt,          │                               │
     │         userName, email, role,            │                               │
     │         vendorId? }                       │                               │
     |<──────────────────────────────────────────│                               │
     │                                           │                               │
     │  [Store JWT in localStorage/session]      │                               │
     │                                           │                               │
     │  Authorization: Bearer {token}            │                               │
     │ ─────────────────────────────────────────>│                               │
     │  [Role-gated endpoint]                    │                               │
     │                                           │                               │
```

---

## 2. Path 1: Admin Login / Vendor Login

```
Actor: Admin/Vendor (browser)
Participant: AuthController (API) | AuthService | UserManager (ASP.NET Identity) | Vendors table | JwtSecurityTokenHandler

Browser            AuthController         AuthService           UserManager          Vendors table       JwtSecurityTokenHandler
   │                     │                     │                    │                     │                      │
   │ POST /api/auth/login (Admin) hoặc /api/auth/vendor-login (Vendor)│                │                     │                      │
   │ {email, password}   │                     │                    │                     │                      │
   │────────────────────>│                     │                    │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │ LoginAsync(email, password)              │                     │                      │
   │                     │────────────────────>│                    │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ FindByEmailAsync(email)                  │                      │
   │                     │                     │──────────────────> │                     │                      │
   │                     │                     │<────────────────── │                     │                      │
   │                     │                     │ AppUser?           │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ CheckPasswordAsync(user, password)       │                      │
   │                     │                     │──────────────────> │                     │                      │
   │                     │                     │<────────────────── │                     │                      │
   │                     │                     │ true/false         │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ [invalid → 401]    │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ GetRolesAsync(user)                      │                      │
   │                     │                     │──────────────────> │                     │                      │
   │                     │                     │<────────────────── │                     │                      │
   │                     │                     │ ["Admin"] or ["Vendor"]                  │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ [role == "Vendor"] │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ GetVendorByUserIdAsync(user.Id)          │                      │
   │                     │                     │─────────────────────────────────────────>│                      │
   │                     │                     │<─────────────────────────────────────────│                      │
   │                     │                     │ Vendor {Status, PoiPointId}?             │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ [Vendor + Status == "Suspended"]         │                      │
   │                     │                     │ 403: "Tài khoản vendor đã ngưng hợp tác" │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ [Vendor + Status != "Approved"]          │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ 403: "Tài khoản đang chờ được duyệt"     │                      │
   │                     │                     │                    │                     │                      │
   │                     │<────────────────────│                    │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │ [Build claims]      │                    │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │  claims = {         │                    │                     │                      │
   │                     │    sub: user.Id,    │                    │                     │                      │
   │                     │    email: email,    │                    │                     │                      │
   │                     │    name: user.Name, │                    │                     │                      │
   │                     │    role: "Admin"/"Vendor",               │                     │                      │
   │                     │    (vendorId: vendor.Id) IF Vendor}      │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │ GenerateJwtToken(claims)                 │                      │
   │                     │                     │─────────────────────────────────────────>│                      │
   │                     │                     │<─────────────────────────────────────────│                      │
   │                     │                     │  { token: "jwt...", expiresAt: datetime }│                      │
   │                     │                     │                    │                     │                      │
   │                     │                     │                    │                     │                      │
   │  200 OK             │                     │                    │                     │                      │
   │ { accessToken,      │                     │                    │                     │                      │
   │   expiresAt,        │                     │                    │                     │                      │
   │   userName, email,  │                     │                    │                     │                      │
   │   role,             │                     │                    │                     │                      │
   │   vendorId? }       │                     │                    │                     │                      │
   │<────────────────────│                     │                    │                     │                      │
   │                     │                     │                    │                     │                      │
```

---

## 3. Path 2: Vendor Registration + Forgot Password

```
Actor: Vendor (browser)
Participant: AuthController | AuthService | UserManager | Vendors table

Browser            AuthController         AuthService           UserManager          Vendors table
   │                     │                     │                    │                     │
   │ POST /api/auth/vendor-register            │                    │                     │
   │ {email, password, businessName, ...}      │                    │                     │
   │────────────────────>│                     │                    │                     │
   │                     │                     │                    │                     │
   │                     │ VendorRegisterAsync(dto)                 │                     │
   │                     │───────────────────> │                    │                     │
   │                     │                     │                    │                     │
   │                     │                     │ CreateAsync(AppUser)                     │
   │                     │                     │──────────────────> │                     │
   │                     │                     │<────────────────── │                     │
   │                     │                     │ IdentityResult     │                     │
   │                     │                     │                    │                     │
   │                     │                     │ [failed → 400]     │                     │
   │                     │                     │                    │                     │
   │                     │                     │ AddToRoleAsync(user, "Vendor")           │
   │                     │                     │───────────────────>│                     │
   │                     │                     │<───────────────────│                     │
   │                     │                     │                    │                     │
   │                     │                     │ CreateVendor(Vendor {                    │
   │                     │                     │   UserId = user.Id,                      │
   │                     │                     │   Status = "Pending",  ← KEY             │
   │                     │                     │   PoiPointId = null,                     │
   │                     │                     │   BusinessName, ...                      │
   │                     │                     │ })                                       │
   │                     │                     │─────────────────────────────────────────>│
   │                     │                     │<─────────────────────────────────────────│
   │                     │                     │  Vendor created                          │
   │                     │                     │                    │                     │
   │                     │                     │                    │                     │
   │  200 OK: "Tài khoản đang chờ được duyệt"  │                    │                     │
   │<────────────────────│                     │                    │                     │
   │ [Vendor CANNOT login until Admin approves]│                    │                     │
   │                     │                     │                    │                     │
```

---

### 3b. Vendor Forgot Password

```
Actor: Vendor (browser)
Participant: AuthController | AuthService | UserManager | Vendors table

Browser            AuthController         AuthService           UserManager          Vendors table
   │                     │                     │                    │                     │
   │ POST /api/auth/vendor-forgot-password     │                    │                     │
   │ { email, phone, newPassword }             │                    │                     │
   │────────────────────>│                     │                    │                     │
   │                     │ ResetVendorPasswordAsync(dto)            │                     │
   │                     │───────────────────> │                    │                     │
   │                     │                     │ FindByEmailAsync(email)                  │
   │                     │                     │──────────────────> │                     │
   │                     │                     │<────────────────── │                     │
   │                     │                     │                    │ Get vendor by UserId│
   │                     │                     │─────────────────────────────────────────>│
   │                     │                     │<─────────────────────────────────────────│
   │                     │                     │ Validate phone + GeneratePasswordResetToken
   │                     │                     │ ResetPasswordAsync(user, token, newPassword)
   │                     │                     │──────────────────> │                     │
   │                     │                     │<────────────────── │                     │
   │ 200 OK: "Khôi phục mật khẩu thành công"   │                    │                     │
   │<────────────────────│                     │                    │                     │
```

---

## 4. Path 3: Device Registration (MAUI App — anonymous)

```
Actor: MauiApp (first launch)
Participant: AuthController | Preferences (MAUI) | AppUser (Device user)

MauiApp              AuthController         Preferences          AppUser
   │                       │                     │                 │
   │ [Get deviceId]        │                     │                 │
   │                       │                     │                 │
   │ Preferences.Get("device_id")                │                 │
   │──────────────────────>│                     │                 │
   │                       │        deviceId?    │                 │
   │<──────────────────────│                     │                 │
   │                       │                     │                 │
   │ [No deviceId → generate]                    │                 │
   │ Guid.NewGuid()        │                     │                 │
   │                       │                     │                 │
   │ Preferences.Set("device_id", deviceId)      │                 │
   │──────────────────────>│                     │                 │
   │                       │                     │                 │
   │                       │                     │                 │
   │ POST /api/auth/device-register              │                 │
   │ {deviceId}            │                     │                 │
   │──────────────────────>│                     │                 │
   │                       │                     │                 │
   │                       │ DeviceRegisterAsync(deviceId)         │
   │                       │────────────────────>│                 │
   │                       │                     │                 │
   │                       │                     │ FindOrCreateUser(
   │                       │                     │   email: "device_{deviceId}
   │                       │                     │    @tastevinhkhanh.local",
   │                       │                     │   role: "Device"
   │                       │                     │ )               │
   │                       │                     │────────────────>│
   │                       │                     │<────────────────│
   │                       │                     │  AppUser created/found
   │                       │                     │                 │
   │                       │                     │ GenerateJwtToken()
   │                       │                     │   claims: { role: "Device",
   │                       │                     │   deviceId: "..." }
   │                       │                     │────────────────>│
   │                       │                     │<────────────────│
   │                       │                     │  token (expires 1 year)
   │                       │                     │                 │
   │                       │                     │                 │
   │  200 OK: { accessToken, expiresIn }         │                 │
   │<────────────────────  │                     │                 │
   │                       │                     │                 │
   │ SaveAccessToken(token)│                     │                 │
   │──────────────────────>│                     │                 │
   │                       │                     │                 │
   │ [Now use Bearer token for /api/audio/{id}]  │                 │
   │                       │                     │                 │
```

---

## 5. Tổng hợp — Role-Based Access Decision Flow

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                           Role-Based Access Decision                             │
└──────────────────────────────────────────────────────────────────────────────────┘

  Request + Bearer Token
            │
            ▼
  ┌─────────────────────┐
  │  [Authorize] attr   │ ─── Anonymous ──────────────────→ Public endpoint (no auth needed)
  └──────────┬──────────┘
             │
             │ Has Bearer token?
             │
       ┌─────┴──────┐
       │  NO        │ ──────────────────────────────────────────────────────────── 401 Unauthorized
       └────────────┘
             │
             │ YES
             ▼
  ┌─────────────────────────┐
  │  JwtBearerMiddleware    │ ─── Invalid/expired ──────────────────────────────── 401
  └──────────┬──────────────┘
             │ Valid JWT
             ▼
  ┌─────────────────────────┐
  │  [Authorize(Roles="X")] │ ─── Role mismatch ───────────────────────────────── 403 Forbidden
  └──────────┬──────────────┘
             │ Role OK
             ▼
  ┌─────────────────────────┐
  │  Controller Action      │ ─── Vendor: check Vendors.Status == "Approved"? ─── 403 (if Pending)
  │  (business logic)       │
  └──────────┬──────────────┘
             │
             ▼
         ✅ Proceed
```

---

## 6. Role & Access Matrix

| Role        | Login endpoint                   | JWT Claims                                | Accessible endpoints                                               |
| ----------- | -------------------------------- | ----------------------------------------- | ------------------------------------------------------------------ |
| `Admin`     | `POST /api/auth/login`           | `sub, email, name, role=Admin`            | All admin endpoints + admin-only audio                             |
| `Vendor`    | `POST /api/auth/vendor-login`    | `sub, email, name, role=Vendor, vendorId` | Vendor-only endpoints (if `Status=Approved`, blocked when `Suspended`) |
| `Device`    | `POST /api/auth/device-register` | `sub, role=Device, deviceId`              | `GET /api/audio/{id}`, `GET /api/sync`, `POST /api/analytics/logs` |
| `Anonymous` | —                                | —                                         | `GET /api/poi`, `GET /api/tour`, `POST /api/sync/playback`         |

---

## 7. Thành phần & File mapping

| Thành phần                  | File trong codebase                                              |
| --------------------------- | ---------------------------------------------------------------- |
| Login + register            | `Api/Controllers/AuthController.cs`                              |
| JWT generation + validation | `Api/Services/AuthService.cs`                                    |
| ASP.NET Identity            | `Api/Services/AuthService.cs` → `UserManager<AppUser>`           |
| Device auth                 | `Api/Services/AuthService.cs` → `GetOrCreateDeviceTokenAsync()`  |
| JWT config                  | `Api/Program.cs` → `AddJwtBearer()`                              |
| Vendor approval/status gate | `Api/Services/AuthService.cs` → `IsVendorApprovedAsync()`, `GetVendorStatusByEmailAsync()` |
| MAUI preferences            | `MauiApp/Services/AudioPlayerService.cs` → `Preferences.Get/Set` |

---

---

# POI Management Flow — Sequence Diagram

> Mô tả luồng quản lý POI: Admin CRUD trực tiếp + Vendor 2-step update (submit → Admin approve/reject).

---

## 1. Tổng quan — 3 Actor Paths

```
Admin ──────────────────────────────────────────────────────────────
  ├── Create POI    → POST /api/poi              → PoiService.CreateAsync()
  ├── Update POI    → PUT /api/poi/{id}           → PoiService.UpdateAsync()
  ├── Delete POI    → DELETE /api/poi/{id}        → PoiService.DeleteAsync()
  ├── Upsert Audio  → PUT /api/poi/{id}/scripts  → AudioScript entity
  └── Manage Vendor Updates → GET/POST /api/admin/pending-updates/{id}/approve

Vendor ─────────────────────────────────────────────────────────────
  ├── Request POI change → PUT /api/vendor/pois/{id}
  │                      → PendingPOIUpdate record (Status=Pending)
  └── Request New POI   → POST /api/vendor/pois
                        → PendingPOIUpdate (PoiPointId=0, Status=Pending)

MauiApp ────────────────────────────────────────────────────────────
  └── Read POIs   → GET /api/sync (anonymous) → SyncService.GetChangesAsync()
```

---

## 2. Path 1: Admin — Create / Update / Delete POI

### 2a. Admin Create POI

```
Actor: Admin
Participant: PoiController | PoiService | AppDbContext | PoiPoint entity

Admin           PoiController           PoiService             AppDbContext        PoiPoint
   │                   │                      │                     │                  │
   │ POST /api/poi     │                      │                     │                  │
   │ {name, lat, lng,  │                      │                     │                  │
   │  triggerRadius,   │                      │                     │                  │
   │  priority, ...}   │                      │                     │                  │
   │──────────────────>│                      │                     │                  │
   │                   │                      │                     │                  │
   │                   │ CreateAsync(CreatePoiRequest)              │                  │
   │                   │─────────────────────>│                     │                  │
   │                   │                      │                     │                  │
   │                   │                      │ new PoiPoint {      │                  │
   │                   │                      │   Name, Latitude,   │                  │
   │                   │                      │   Longitude,        │                  │
   │                   │                      │  TriggerRadiusMeters│                  │
   │                   │                      │  Priority,          │                  │
   │                   │                      │  IsActive = true    │                  │
   │                   │                      │ }                   │                  │
   │                   │                      │────────────────────>│                  │
   │                   │                      │                     │ Insert           │
   │                   │                      │<────────────────────│                  │
   │                   │                      │                     │                  │
   │  201 Created      │                      │                     │                  │
   │ { poiId, ... }    │                      │                     │                  │
   |<───────────────── │                      │                     │                  │
   │                   │                      │                     │                  │
```

### 2b. Admin Update POI

```
Admin           PoiController           PoiService             AppDbContext
   │                   │                      │                     │
   │ PUT /api/poi/{id} │                      │                     │
   │ {name, lat, lng,  │                      │                     │
   │  triggerRadius,   │                      │                     │
   │  priority, ...}   │                      │                     │
   │──────────────────>│                      │                     │
   │                   │                      │                     │
   │                   │ UpdateAsync(id, UpdatePoiRequest)          │
   │                   │─────────────────────>│                     │
   │                   │                      │                     │
   │                   │                      │ GetPoiPoint(id)     │
   │                   │                      │────────────────────>│
   │                   │                      │<────────────────────│
   │                   │                      │ poi?                │
   │                   │                      │                     │
   │                   │                      │ [not found → 404]   │
   │                   │                      │                     │
   │                   │                      │ Apply changes:      │
   │                   │                      │ poi.Name = ...,     │
   │                   │                      │ poi.Latitude = ...  │
   │                   │                      │ poi.TriggerRadius = │
   │                   │                      │ poi.Priority = ...  │
   │                   │                      │────────────────────>│
   │                   │                      │<────────────────────│
   │                   │                      │  SaveChangesAsync   │
   │                   │                      │                     │
   │                   │                      │                     │
   │  200 OK           │                      │                     │
   |<──────────────────│                      │                     │
   │                   │                      │                     │
```

### 2c. Admin Delete POI

```
Admin           PoiController           PoiService             AppDbContext
   │                   │                      │                     │
   │ DELETE /api/poi/{id}                     │                     │
   │──────────────────>│                      │                     │
   │                   │                      │                     │
   │                   │ DeleteAsync(id)      │                     │
   │                   │─────────────────────>│                     │
   │                   │                      │                     │
   │                   │                      │ GetPoiPoint(id)     │
   │                   │                      │────────────────────>│
   │                   │                      │<────────────────────│
   │                   │                      │ poi?                │
   │                   │                      │                     │
   │                   │                      │ [soft delete]       │
   │                   │                      │ poi.IsActive = false││
   │                   │                      │ poi.UpdatedAt = now │
   │                   │                      │────────────────────>│
   │                   │                      │<────────────────────│
   │                   │                      │                     │
   │  204 No Content   │                      │                     │
   |<──────────────────│                      │                     │
   │                   │                      │                     │
```

---

## 3. Path 2: Admin — Upsert Audio Script

```
Actor: Admin
Participant: PoiController | AppDbContext | AudioScript entity

Admin           PoiController           AppDbContext        AudioScript
   │                   │                      │                  │
   │ PUT /api/poi/{poiId}/scripts             │                  │
   │ {languageCode, ttsScript, audioFilePath} │                  │
   │──────────────────>│                      │                  │
   │                   │                      │                  │
   │                   │ GetAudioScript(poiId, langCode)         │
   │                   │─────────────────────>│                  │
   │                   │<─────────────────────│                  │
   │                   │ existing?            │                  │
   │                   │                      │                  │
   │                   │ [exists → UPDATE]    │                  │
   │                   │ script.TtsScript = ...,                 │
   │                   │ script.AudioFilePath = ...              │
   │                   │                      │                  │
   │                   │ [not exists → INSERT]│                  │
   │                   │ new AudioScript {    │                  │
   │                   │   PoiPointId,        │                  │
   │                   │   LanguageCode,      │                  │
   │                   │   TtsScript,         │                  │
   │                   │   AudioFilePath      │                  │
   │                   │ }                    │                  │
   │                   │─────────────────────>│                  │
   │                   │                      │ Insert/Update    │
   │                   │<─────────────────────│                  │
   │                   │                      │                  │
   │  200 OK           │                      │                  │
   |<──────────────────│                      │                  │
   │                   │                      │                  │
```

---

## 4. Path 3: Vendor — 2-Step POI Update

> **Bước 1**: Vendor gửi yêu cầu thay đổi → tạo `PendingPOIUpdate` (chưa áp dụng)
> **Bước 2**: Admin duyệt → áp dụng thay đổi vào `PoiPoint`, `RestaurantImage`, `AudioScript`

### Step 1: Vendor Submit Update Request

```
Actor: Vendor
Participant: VendorController | AppDbContext | PendingPOIUpdate entity | Vendors table

Vendor            VendorController          AppDbContext          Vendors table
   │                    │                     │                    │
   │ PUT /api/vendor/pois/{id}                │                    │
   │ { payload (JSON),                        │                    │
   │   imagesPayload (JSON),                  │                    │
   │   scriptsPayload (JSON) }                │                    │
   │──────────────────>│                      │                    │
   │                   │                      │                    │
   │                   │ GetVendorByUserId(userId)                 │
   │                   │──────────────────────────────────────────>│
   │                   │<──────────────────────────────────────────│
   │                   │ vendor {Status, PoiPointId}?              │
   │                   │                      │                    │
   │                   │[Status != "Approved"]│                    │
   │                   │ 403: "Tài khoản chưa được duyệt"          │
   │                   │                      │                    │
   │                   │ [PoiPointId != requestedId] │             │
   │                   │ 403: "Không có quyền"                     │
   │                   │                      │                    │
   │                   │ [Get existing PoiPoint] │                 │
   │                   │─────────────────────>│                    │
   │                   │<─────────────────────│                    │
   │                   │ poi?                 │                    │
   │                   │                      │                    │
   │                   │ UpsertPendingUpdate( │                    │
   │                   │   VendorId = vendor.Id,                   │
   │                   │   PoiPointId = poiId,│                    │
   │                   │   Payload = JSON,    │                    │
   │                   │   ImagesPayload = JSON,                   │
   │                   │   ScriptsPayload = JSON,                  │
   │                   │   Status = "Pending",│                    │
   │                   │   RequestedAt = now  │                    │
   │                   │ )                    │                    │
   │                   │─────────────────────>│                    │
   │                   │                      │ Insert/Update PendingPOIUpdate
   │                   │<─────────────────────│                    │
   │                   │                      │                    │
   │  200: { pendingUpdateId }                │                    │
   |<──────────────────│                      │                    │
   │ [Update submitted — pending Admin review]│                    │
   │                   │                      │                    │
```

### Step 2: Admin Review — Approve (Update existing POI)

```
Actor: Admin
Participant: AdminVendorController | AppDbContext | PoiPoint | RestaurantImage | AudioScript

Admin             AdminVendorController       AppDbContext         PoiPoint        RestaurantImage      AudioScript
   │                      │                       │                   │                  │                  │
   │ GET /api/admin/pending-updates               │                   │                  │                  │
   │─────────────────────>│                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ List pending updates  │                   │                  │                  │
   │                      │<──────────────────────│                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │ 200: [PendingPOIUpdate, ...]                 │                   │                  │                  │
   │<──────────────────── │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │ GET /api/admin/pending-updates/{id}          │                   │                  │                  │
   │─────────────────────>│                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ GetDetail(id)         │                   │                  │                  │
   │                      │ Parse(Payload, ImagesPayload, ScriptsPayload)                │                  │
   │                      │ DetermineChangeType:  │                   │                  │                  │
   │                      │   PoiPointId == 0 → "poi_created"         │                  │                  │
   │                      │   ImagesPayload ≠ {} → "image_uploaded"   │                  │                  │
   │                      │   ScriptsPayload ≠ {} → "script_updated"  │                  │                  │
   │                      │   Payload ≠ {} → "poi_updated"            │                  │                  │
   │                      │                       │                   │                  │                  │
   │ 200: full detail     │                       │                   │                  │                  │
   |<──────────────────── │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │ POST /api/admin/pending-updates/{id}/approve │                   │                  │                  │
   │─────────────────────>│                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ [PoiPointId != 0 — UPDATE existing POI]   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ GetPoiPoint(PoiPointId)                   │                  │                  │
   │                      │──────────────────────>│                   │                  │                  │
   │                      │<──────────────────────│                   │                  │                  │
   │                      │ poi                   │                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ ApplyPayload(poi, Payload JSON)           │                  │                  │
   │                      │ poi.Name = ..., poi.Latitude = ..., etc.  │                  │                  │
   │                      │──────────────────────>│                   │                  │                  │
   │                      │<──────────────────────│                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ [ImagesPayload not empty]                 │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ DeleteExistingImages(PoiPointId)          │                  │                  │
   │                      │──────────────────────>│                   │                  │                  │
   │                      │<──────────────────────│                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ [for each img in ImagesPayload]           │                  │                  │
   │                      │ InsertRestaurantImage(                    │                  │                  │
   │                      │   PoiPointId, ImageUrl,                   │                  │                  │
   │                      │   IsPrimary, SortOrder)                   │                  │                  │
   │                      │──────────────────────>│                   │═════════════════>│                  │
   │                      │<──────────────────────│                   │══════════════════│                  │
   │                      │                       │                   │                  │                  │
   │                      │ [ScriptsPayload not empty]                │                  │                  │
   │                      │                       │                   │                  │                  │
   │                      │ UpsertAudioScripts(PoiPointId, ScriptsPayload)               │                  │
   │                      │──────────────────────>│                   │                  │─────────────────>│
   │                      │<──────────────────────│                   │                  │<─────────────────│
   │                      │                       │                   │                  │                  │
   │                      │ UpdatePendingUpdate(Status="Approved")    │                  │                  │
   │                      │──────────────────────>│                   │                  │                  │
   │                      │<──────────────────────│                   │                  │                  │
   │                      │                       │                   │                  │                  │
   │ 200 OK               │                       │                   │                  │                  │
   |<──────────────────── │                       │                   │                  │                  │
   │                      │                       │                   │                  │                  │
```

### Step 2b: Admin Approve — Create New POI (Vendor Request)

```
Actor: Admin
Participant: AdminVendorController | AppDbContext | PoiPoint | Vendors table

Admin             AdminVendorController       AppDbContext         PoiPoint         Vendors
   │                      │                       │                   │                  │
   │ POST /api/admin/pending-updates/{id}/approve (PoiPointId == 0)   │                  │
   │─────────────────────>│                       │                   │                  │
   │                      │                       │                   │                  │
   │                      │ [PoiPointId == 0 — CREATE new POI]        │                  │
   │                      │                       │                   │                  │
   │                      │ new PoiPoint {        │                   │                  │
   │                      │   ApplyPayload(pendingUpdate.Payload),    │                  │
   │                      │   IsActive = true,    │                   │                  │
   │                      │   CreatedAt = now     │                   │                  │
   │                      │ }                     │                   │                  │
   │                      │──────────────────────>│                   │ Insert           │
   │                      │<──────────────────────│                   │ newPoi.Id        │
   │                      │ newPoi.Id             │                   │                  │
   │                      │                       │                   │                  │
   │                      │ [ImagesPayload]       │                   │                  │
   │                      │ InsertRestaurantImages(...)               │                  │
   │                      │──────────────────────>│                   │                  │
   │                      │<──────────────────────│                   │                  │
   │                      │                       │                   │                  │
   │                      │ [ScriptsPayload]      │                   │                  │
   │                      │ UpsertAudioScripts(newPoi.Id, ...)        │                  │
   │                      │──────────────────────>│                   │                  │
   │                      │<──────────────────────│                   │                  │
   │                      │                       │                   │                  │
   │                      │ AssignVendorToPoi(pendingUpdate.VendorId, newPoi.Id)         │
   │                      │──────────────────────>│                   │                  │
   │                      │                                           │─────────────────>│
   │                      │<──────────────────────│                   │<─────────────────│
   │                      │                       │                   │                  │
   │                      │ UpdatePendingUpdate(Status="Approved")    │                  │
   │                      │──────────────────────>│                   │                  │
   │                      │<──────────────────────│                   │                  │
   │                      │                       │                   │                  │
   │ 200 OK: { newPoiId } │                       │                   │                  │
   |<─────────────────────│                       │                   │                  │
   │                      │                       │                   │                  │
```

### Step 2c: Admin Reject

```
Admin             AdminVendorController       AppDbContext
   │                      │                       │
   │ POST /api/admin/pending-updates/{id}/reject  │
   │─────────────────────>│                       │
   │                      │                       │
   │                      │ UpdatePendingUpdate(Status="Rejected", RejectedReason)
   │                      │──────────────────────>│
   │                      │<──────────────────────│
   │                      │                       │
   │ 200 OK               │                       │
   |<──────────────────── │                       │
   │ [No changes applied to POI]                  │
```

---

## 5. Tổng hợp — Vendor 2-Step Update Flow (single view)

```
┌──────────┐   ┌───────────────────┐  ┌─────────────┐  ┌──────────┐  ┌─────────────────────┐  ┌────────────┐
│  Vendor  │   │  VendorController │  │AppDbContext │  │  Admin   │  │AdminVendorController│  │  PoiPoint  │
└────┬─────┘   └────────┬──────────┘  └──────┬──────┘  └────┬─────┘  └──────────┬──────────┘  └────┬───────┘
     │                  │                    │              │                   │                  │              │
     │ PUT /api/vendor/pois/{id}             │              │                   │                  │              │
     │ {Payload, ImagesPayload, ScriptsPayload}             │                   │                  │              │
     │────────────────> │                    │              │                   │                  │              │
     │                  │ ValidateVendorApproved()          │                   │                  │              │
     │                  │──────────────────────────────────>│                   │                  │              │
     │                  │<──────────────────────────────────│                   │                  │              │
     │                  │                    │              │                   │                  │              │
     │                  │ UpsertPendingPOIUpdate(           │                   │                  │              │
     │                  │   VendorId, PoiPointId,           │                   │                  │              │
     │                  │   Payload/Images/Scripts JSON,    │                   │                  │              │
     │                  │   Status=Pending)                 │                   │                  │              │
     │                  │───────────────────>│              │                   │                  │              │
     │                  │                    │ Insert       │                   │                  │              │
     │                  │<───────────────────│              │                   │                  │              │
     │                  │                    │              │                   │                  │              │
     │ 200: {pendingId} │                    │              │                   │                  │              │
     |<──────────────── │                    │              │                   │                  │              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │    GET /api/admin/pending-updates/{id}              │              │
     │                  │                    │              │<──────────────────│                  │              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │  200: full detail (changeType)       │              │
     │                  │                    │              │──────────────────>│                  │              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │                   │ [Approve click]  │              │
     │                  │                    │              │                   │─────────────────>│              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │                   │ GetPoiPoint(PoiPointId)│        │
     │                  │                    │              │                   │─────────────────>│              │
     │                  │                    │              │                   │<─────────────────│              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │                   │ ApplyPayload(PoiPoint, JSON)│   │
     │                  │                    │              │                   │─────────────────>│              │
     │                  │                    │              │                   │<─────────────────│              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │                   │ ReplaceRestaurantImages(PoiPointId)
     │                  │                    │              │                   │─────────────────>│              │
     │                  │                    │              │                   │<─────────────────│              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │                   │ UpsertAudioScripts(PoiPointId)│ │
     │                  │                    │              │                   │─────────────────>│              │
     │                  │                    │              │                   │<─────────────────│              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │                   │ UpdatePending(Status=Approved)│││
     │                  │                    │              │                   │─────────────────>│              │
     │                  │                    │              │                   │<─────────────────│              │
     │                  │                    │              │                   │                  │              │
     │                  │                    │              │  200 OK           │                  │              │
     │                  │                    │              │<──────────────────│                  │              │
     │                  │                    │              │ [Changes applied to POI]             │
```

---

## 6. Change Type Detection

| Điều kiện trong `PendingPOIUpdate` | `changeType`     | Admin action                       |
| ---------------------------------- | ---------------- | ---------------------------------- |
| `PoiPointId == 0`                  | `poi_created`    | Tạo POI mới + assign cho Vendor    |
| `Payload` có dữ liệu               | `poi_updated`    | Áp dụng thay đổi vào `PoiPoint`    |
| `ImagesPayload` có dữ liệu         | `image_uploaded` | Xóa ảnh cũ → insert ảnh mới        |
| `ScriptsPayload` có dữ liệu        | `script_updated` | Upsert `AudioScript` theo ngôn ngữ |

---

## 7. Thành phần & File mapping

| Thành phần            | File trong codebase                                                                |
| --------------------- | ---------------------------------------------------------------------------------- |
| POI CRUD (Admin)      | `Api/Controllers/PoiController.cs`                                                 |
| Vendor POI update     | `Api/Controllers/VendorController.cs` → `PUT /api/vendor/pois/{id}`                |
| Admin review pending  | `Api/Controllers/AdminVendorController.cs` → `GET/POST /api/admin/pending-updates` |
| POI business logic    | `Api/Services/PoiService.cs`                                                       |
| Pending update entity | `Shared/Models/PendingPOIUpdate.cs`                                                |
| POI entity            | `Shared/Models/PoiPoint.cs`                                                        |
| Vendor entity         | `Shared/Models/Vendor.cs`                                                          |
| MauiApp reads POI     | `Api/Controllers/SyncController.cs` → `GET /api/sync`                              |

---

## 9. Ghi chú (Audio Playback)

- **3-Tier Audio Playback**: Tier 1 (local cached) → Tier 2 (download from server) → Tier 3 (device TTS fallback)
- **5-min Cooldown**: Mỗi POI chỉ trigger 1 lần trong 5 phút, tránh phát lại khi user đi qua đi lại
- **Delta Sync**: `GET /api/sync?lastSyncAt=...` chỉ gửi POI thay đổi sau timestamp, tiết kiệm bandwidth
- **Device JWT**: App đăng ký device → nhận JWT → dùng để download audio file (protected endpoint)
- **Fire-and-forget log sync**: Upload playback logs không block main thread, chạy background sau khi phát xong

---

# Device Registration Flow — Sequence Diagram

> Mô tả luồng MAUI App đăng ký device → nhận JWT (Device token) → dùng Bearer token để download audio file bảo vệ qua `GET /api/audio/{id}`.

---

## 1. Tổng quan — Device Auth Lifecycle

```
┌─────────────┐   First launch      ┌──────────────────┐   device_id not found  ┌─────────────────────┐
│  MauiApp    │ ─────────────────>  │AudioPlayerService│ ─────────────────────> │  Preferences        │
│             │                     │                  │                        │  (device_id,        │
│             │                     │                  │                        │   device_token)     │
└──────┬──────┘                     └────────┬─────────┘                        └──────────┬──────────┘
       │                                     │                                        │
       │                                     │ GUID.NewGuid()                         │
       │                                     │───────────────────────────────────────>│
       │                                     │                                        │
       │                                     │                                        │
       │  POST /api/auth/device-register     │                                        │
       │  {deviceId}                         │                                        │
       │────────────────────────────────────>│                                        │
       │                                     │                                        │
       │                                     │ [FindOrCreate AppUser                  │
       │                                     │  email=device_{id}                     │
       │                                     │  @tastevinhkhanh.local,                │
       │                                     │  Role="Device"]                        │
       │                                     │                                        │
       │                                     │  Generate JWT (1 year)                 │
       │                                     │                                        │
       │  { accessToken, expiresIn }         │                                        │
       │<────────────────────────────────────│                                        │
       │                                     │                                        │
       │  Save to Preferences                │                                        │
       │ ───────────────────────────────────>│                                        │
       │                                     │                                        │
       │  [Every audio download from now on] │                                        │
       │  Authorization: Bearer {device_token}                                        │
       │ ───────────────────────────────────>│ AudioController                        │
       │                                     │  [GET /api/audio/{scriptId}]           │
```

---

## 2. Chi tiết từng bước

### Bước 1: Khởi tạo device ID (first launch)

```
Actor: MauiApp
Participant: AudioPlayerService | Preferences

AudioPlayerService    Preferences
       │                   │
       │ GetDeviceId()     │
       │──────────────────>│
       │                   │
       │ [device_id exists?]
       │<──────────────────│
       │ YES ──> use existing
       │                   │
       │ NO ──> generate new
       │ Guid.NewGuid()    │
       │                   │
       │ Set("device_id", guid)
       │──────────────────>│
       │                   │
```

### Bước 2: Đăng ký device với server → nhận JWT

```
Actor: MauiApp
Participant: AudioPlayerService | AuthController (API) | AppDbContext | AppUser

AudioPlayerService    AuthController(API)       AppDbContext          AppUser
       │                     │                     │                   │
       │ RegisterDeviceAsync(deviceId)             │                   │
       │────────────────────>│                     │                   │
       │                     │                     │                   │
       │                     │ DeviceRegisterAsync(deviceId)           │
       │                     │────────────────────>│                   │
       │                     │                     │                   │
       │                     │ FindByNameAsync(    │                   │
       │                     │   "device_{id}@     │                   │
       │                     │    tastevinhkhanh.local")               │
       │                     │────────────────────>│                   │
       │                     │<────────────────────│                   │
       │                     │ appUser?            │                   │
       │                     │                     │                   │
       │                     │ [user not found]    │                   │
       │                     │                     │                   │
       │                     │ CreateAsync(AppUser {                   │
       │                     │   UserName = ...,   │                   │
       │                     │   Email = ...,      │                   │
       │                     │   // no password    │                   │
       │                     │ })                  │                   │
       │                     │────────────────────>│                   │
       │                     │<────────────────────│                   │
       │                     │                     │                   │
       │                     │ AddToRoleAsync(user, "Device")          │
       │                     │────────────────────>│                   │
       │                     │<────────────────────│                   │
       │                     │                     │                   │
       │                     │ [Build claims]      │                   │
       │                     │ claims = {          │                   │
       │                     │   sub: user.Id,     │                   │
       │                     │   role: "Device",   │                   │
       │                     │   deviceId: deviceId│                   │
       │                     │ }                   │                   │
       │                     │                     │                   │
       │                     │ GenerateJwtToken(   │                   │
       │                     │   claims,           │                   │
       │                     │   expiresIn: 365 days                   │
       │                     │ )                   │                   │
       │                     │────────────────────>│                   │
       │                     │<────────────────────│                   │
       │                     │  token (1 year)     │                   │
       │                     │                     │                   │
       │  200: { accessToken,│                     │                   │
       │        expiresIn: 31536000 }              │                   │
       │<────────────────────│                     │                   │
       │                     │                     │                   │
       │ SaveAccessToken(token)                    │                   │
       │────────────────────>│                     │                   │
       │                     │                     │                   │
```

### Bước 3: Download audio với Bearer token

```
Actor: MauiApp
Participant: AudioPlayerService | AudioController (API) | FileSystem (wwwroot/audio)

AudioPlayerService    AudioController(API)    AppDbContext         FileSystem
       │                     │                     │                   │
       │ DownloadAudioAsync(scriptId)              │                   │
       │                     │                     │                   │
       │ GET /api/audio/{scriptId}                 │                   │
       │ Authorization: Bearer {device_token}      │                   │
       │────────────────────>│                     │                   │
       │                     │ [JwtBearer validates token]             │
       │                     │                     │                   │
       │                     │ GetAudioScript(scriptId)                │
       │                     │────────────────────>│                   │
       │                     │<────────────────────│                   │
       │                     │ audioScript?        │                   │
       │                     │                     │                   │
       │                     │ [not found → 404]   │                   │
       │                     │                     │                   │
       │                     │ [audioScript.AudioFilePath is empty?]   │
       │                     │ 404: "Audio file not set"               │
       │                     │                     │                   │
       │                     │ ReadFileAsync(audioFilePath)            │
       │                     │────────────────────>│                   │
       │                     │<────────────────────│                   │
       │                     │  file bytes (mp3/wav)                   │
       │                     │                     │                   │
       │                     │  200 OK (audio/mp3) │                   │
       │<────────────────────│                     │                   │
       │ audio bytes         │                     │                   │
       │                     │                     │                   │
       │ Save to cache:      │                     │                   │
       │ FileSystem/AppDataDirectory/              │                   │
       │ audio/{poiId}_{lang}.mp3                  │                   │
       │────────────────────>│                     │                   │
       │                     │                     │                   │
       │ UpdateLocalAudioPath(scriptId, localPath) │                   │
       │                     │                     │                   │
```

### Bước 4: Token refresh (khi hết hạn)

```
Actor: MauiApp
Participant: AudioPlayerService | Preferences | AuthController (API)

AudioPlayerService    Preferences          AuthController(API)
       │                    │                     │
       │ [on 401 response]  │                     │
       │                    │                     │
       │ GetAccessToken()   │                     │
       │───────────────────>│                     │
       │                    │                     │
       │ RegisterDeviceAsync(deviceId) [re-use existing device]│
       │─────────────────────────────────────────>│
       │                    │                     │
       │                    │  [user already exists — refresh JWT]│
       │                    │                     │
       │                    │  Generate new JWT   │
       │                    │<────────────────────│
       │                    │                     │
       │ SaveAccessToken(newToken)                │
       │───────────────────>│                     │
       │                    │                     │
       │ Retry original request with new token    │
       │                    │                     │
```

---

## 3. Tổng hợp — Full Device Registration Lifecycle (single view)

```
┌──────────────┐  ┌─────────────────────┐  ┌────────────────┐  ┌───────────────┐  ┌────────────────────┐  ┌───────────────────┐
│   MauiApp    │  │  AudioPlayerService │  │  Preferences   │  │ AuthController│  │     AppDbContext   │  │  AudioController  │
└───────┬──────┘  └──────────┬──────────┘  └───────┬────────┘  └───────┬───────┘  └──────────┬─────────┘  └────────┬──────────┘
        │                    │                     │                   │                     │                     │
        │ [App Launch]       │                     │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │ GetDeviceId()       │                   │                     │                     │
        │                    │────────────────────>│                   │                     │                     │
        │                    │<────────────────────│                   │                     │                     │
        │                    │ deviceId (or new)   │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │ RegisterDeviceAsync(deviceId)           │                     │                     │
        │                    │────────────────────────────────────────>│                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │                     │                   │ FindOrCreateUser("device_{id}@...")       │
        │                    │                     │                   │────────────────────>│                     │
        │                    │                     │                   │<────────────────────│                     │
        │                    │                     │                   │ AppUser (Device role)│                    │
        │                    │                     │                   │                     │                     │
        │                    │                     │                   │ GenerateJwtToken(role=Device, 1yr)        │
        │                    │                     │                   │────────────────────>│                     │
        │                    │                     │                   │<────────────────────│                     │
        │                    │                     │                   │  token              │                     │
        │                    │                     │                   │                     │                     │
        │  { accessToken }   │                     │                   │                     │                     │
        │<───────────────────│                     │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │ SaveAccessToken(token)                   │                   │                     │                     │
        │─────────────────────────────────────────>│                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │  [Audio needed — trigger Geofence]       │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │ DownloadAudioAsync(scriptId)            │                     │                     │
        │                    │                     │                   │                     │                     │
        │                    │ GET /api/audio/{id} │                   │                     │                     │
        │                    │ Authorization: Bearer {token}           │                     │                     │
        │                    │────────────────────────────────────────────────────────────────────────────────────>│
        │                    │                     │                   │                     │   GET /api/audio/{id}
        │                    │                     │                   │                     │<────────────────────│
        │                    │                     │                   │                     │  200: audio bytes   │
        │                    │                     │                   │                     │────────────────────>│
        │                    │<────────────────────────────────────────────────────────────────────────────────────│
        │  audio bytes       │                     │                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │ Save to local cache│                     │                   │                     │                     │
        │─────────────────────────────────────────>│                   │                     │                     │
        │                    │                     │                   │                     │                     │
        │ PlayLocalFile()    │                     │                   │                     │                     │
        │                    │                     │                   │                     │                     │
```

---

## 4. Security Model

| Khía cạnh          | Chi tiết                                                              |
| ------------------ | --------------------------------------------------------------------- |
| **User identity**  | Device user: `device_{guid}@tastevinhkhanh.local` — không có password |
| **Role**           | `"Device"` — chỉ có quyền download audio + sync + log playback        |
| **Token lifetime** | **1 năm** — không cần refresh thường xuyên                            |
| **Scope**          | Không thể access Vendor/Admin endpoints dù có token                   |
| **On 401**         | Tự động re-register → nhận token mới → retry                          |
| **Offline**        | Token lưu trong `Preferences`, dùng được khi offline để decrypt cache |

---

## 5. Thành phần & File mapping

| Thành phần                   | File trong codebase                                                                        |
| ---------------------------- | ------------------------------------------------------------------------------------------ |
| Device registration endpoint | `Api/Controllers/AuthController.cs` → `POST /api/auth/device-register`                     |
| Device user creation         | `Api/Services/AuthService.cs` → `GetOrCreateDeviceTokenAsync()`                            |
| MAUI: get/set device ID      | `MauiApp/Services/AudioPlayerService.cs` → `Preferences.Get/Set("device_id")`              |
| MAUI: get/set token          | `MauiApp/Services/AudioPlayerService.cs` → `Preferences.Get/Set("device_token")`           |
| Audio file endpoint          | `Api/Controllers/AudioController.cs` → `GET /api/audio/{id}` `[Authorize(Roles="Device")]` |
| JWT config                   | `Api/Program.cs` → `AddJwtBearer()` với device policy                                      |
| Cache storage                | `FileSystem.AppDataDirectory/audio/{poiId}_{lang}.mp3`                                     |

---

## 10. Ghi chú (Audio Playback)

- **3-Tier Audio Playback**: Tier 1 (local cached) → Tier 2 (download from server) → Tier 3 (device TTS fallback)
- **5-min Cooldown**: Mỗi POI chỉ trigger 1 lần trong 5 phút, tránh phát lại khi user đi qua đi lại
- **Delta Sync**: `GET /api/sync?lastSyncAt=...` chỉ gửi POI thay đổi sau timestamp, tiết kiệm bandwidth
- **Device JWT**: App đăng ký device → nhận JWT → dùng để download audio file (protected endpoint)
- **Fire-and-forget log sync**: Upload playback logs không block main thread, chạy background sau khi phát xong
