using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Maps;
using TasteVinhKhanh.MauiApp.Converters;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class MapViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly LocationService _location;
    private readonly GeofenceEngine _geofence;
    private readonly SyncService _sync;
    private readonly NarrationEngine _narration;
    private readonly LocalizationService _i18n;

    [ObservableProperty] private List<LocalPoi> _pois = new();
    [ObservableProperty] private LocalPoi? _nearestPoi;
    [ObservableProperty] private LocalPoi? _selectedPoi;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _syncDetail = "";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _currentLanguage = "vi";
    [ObservableProperty] private string _poiCountLabel = "";
    [ObservableProperty] private Location? _currentUserLocation;
    [ObservableProperty] private int? _nearestPoiId;

    // ── Bindable translated strings ──
    [ObservableProperty] private string _tHeader = "";
    [ObservableProperty] private string _tPoiListTitle = "";
    [ObservableProperty] private string _tPoiListSub = "";
    [ObservableProperty] private string _tLoading = "";
    [ObservableProperty] private string _tLoadingHint = "";
    [ObservableProperty] private string _tHotSeller = "";
    [ObservableProperty] private string _tHasNarration = "";
    [ObservableProperty] private string _tReady = "";
    [ObservableProperty] private string _tSyncing = "";
    [ObservableProperty] private string _tNavHome = "";
    [ObservableProperty] private string _tNavMap = "";
    [ObservableProperty] private string _tNavAudio = "";
    [ObservableProperty] private string _tNavSettings = "";

    // Favorites stored as comma-separated IDs in Preferences
    private static readonly string FavKey = "favorite_pois";
    private HashSet<int> _favoriteIds = new();

    /// <summary>
    /// Sự kiện bắn khi vị trí người dùng thay đổi — dùng bởi MapPage.
    /// </summary>
    public event Action<Location>? LocationChanged;

    public MapViewModel(AppDatabase db, LocationService location,
        GeofenceEngine geofence, SyncService sync, NarrationEngine narration,
        LocalizationService i18n)
    {
        _db = db;
        _location = location;
        _geofence = geofence;
        _sync = sync;
        _narration = narration;
        _i18n = i18n;

        RefreshTexts();

        // Load favorites
        LoadFavorites();
        FavoriteIconConverter.IsFavoriteFunc = IsFavorite;

        // Lắng nghe GPS update
        _location.LocationUpdated += OnLocationUpdated;

        // Lắng nghe khi ngôn ngữ thay đổi
        _i18n.LanguageChanged += () => {
            RefreshTexts();
            UpdateStatusMessages();
        };

        // Lắng nghe khi bắt đầu phát
        _narration.NarrationStarted += name =>
            StatusMessage = $"{_i18n.T("Map_Playing")}{name}";

        // Lắng nghe khi phát xong
        _narration.NarrationFinished += () =>
            StatusMessage = _i18n.T("Map_NarrationReady");

        // Lắng nghe khi POI được kích hoạt (gần)
        _geofence.PoiTriggered += (poi, dist) =>
        {
            NearestPoi = poi;
            NearestPoiId = poi.Id;
            StatusMessage = $"{_i18n.T("Map_Near")}{poi.Name} ({dist:F0}m)";
        };
    }

    private void UpdateStatusMessages()
    {
        // Cập nhật lại tất cả status message theo ngôn ngữ mới
        var current = StatusMessage;
        if (current.StartsWith(_i18n.T("Map_Playing").Replace(" ", "")))
            StatusMessage = $"{_i18n.T("Map_Playing")}{nearestPoi?.Name ?? ""}";
        else if (current.StartsWith(_i18n.T("Map_Near")))
            StatusMessage = current; // giữ nguyên vì tên quán không dịch
    }

    private LocalPoi? nearestPoi;

    private void LoadFavorites()
    {
        var stored = Preferences.Get(FavKey, "");
        _favoriteIds = string.IsNullOrEmpty(stored)
            ? new HashSet<int>()
            : new HashSet<int>(stored.Split(',').Select(int.Parse));
    }

    private void SaveFavorites()
    {
        Preferences.Set(FavKey, string.Join(",", _favoriteIds));
    }

    public bool IsFavorite(int poiId) => _favoriteIds.Contains(poiId);

    [RelayCommand]
    public void ToggleFavorite(int poiId)
    {
        if (_favoriteIds.Contains(poiId))
            _favoriteIds.Remove(poiId);
        else
            _favoriteIds.Add(poiId);
        SaveFavorites();
        OnPropertyChanged(nameof(Pois));
    }

    [RelayCommand]
    public async Task InitAsync()
    {
        IsLoading = true;
        StatusMessage = _i18n.T("Map_Syncing");
        SyncDetail = "";

        await _db.InitAsync();

        // Sync từ server (nếu API chạy)
        var result = await _sync.SyncPoisAsync();

        // Hiện chi tiết sync
        if (!result.Success)
        {
            SyncDetail = $"⚠️ {_i18n.T("Map_NoData")}";
            StatusMessage = "⚠️ Không sync được";
        }
        else if (result.FromCache)
        {
            SyncDetail = $"📴 {_i18n.T("Map_Offline")}{result.UpdatedCount} điểm";
            StatusMessage = $"{_i18n.T("Map_Offline")}{result.UpdatedCount} điểm";
        }
        else
        {
            SyncDetail = $"✅ {result.Message}";
            StatusMessage = result.UpdatedCount > 0
                ? $"{_i18n.T("Map_Synced")}{result.UpdatedCount} điểm"
                : "";
        }

        // Load POI từ SQLite
        var all = await _db.GetAllPoisAsync();
        Pois = all;
        PoiCountLabel = $"{all.Count} quán";

        // Nếu không load được gì → hiện cảnh báo
        if (all.Count == 0)
        {
            SyncDetail += "\n💡 Kiểm tra: API đã chạy chưa?";
            StatusMessage = _i18n.T("Map_NoData");
        }

        // Bắt đầu theo dõi GPS
        await _location.StartAsync();
        IsLoading = false;

        // Upload log cũ nếu có mạng
        _ = _sync.UploadPendingLogsAsync();
    }

    private async void OnLocationUpdated(Location location)
    {
        CurrentUserLocation = location;
        LocationChanged?.Invoke(location);
        await _geofence.CheckLocationAsync(location);
    }

    public void UpdateNearbyHighlight(Location location)
    {
        if (Pois.Count == 0) return;

        LocalPoi? nearest = null;
        double minDist = double.MaxValue;

        foreach (var poi in Pois)
        {
            var dist = CalculateDistance(
                location.Latitude, location.Longitude,
                poi.Latitude, poi.Longitude);

            if (dist < minDist)
            {
                minDist = dist;
                nearest = poi;
            }
        }

        if (nearest != null)
        {
            NearestPoiId = nearest.Id;
            nearestPoi = nearest;
        }
    }

    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                   * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    [RelayCommand]
    public void ChangeLanguage(string lang)
    {
        CurrentLanguage = lang;
        _i18n.SetLanguage(lang);
        _narration.CurrentLanguage = lang;
    }

    [RelayCommand]
    public async Task GoToDetail(int poiId)
    {
        await Shell.Current.GoToAsync($"PoiDetailPage?poiId={poiId}");
    }

    [RelayCommand]
    public async Task GoToHome()
        => await Shell.Current.GoToAsync("//main");

    [RelayCommand]
    public async Task GoToAudio()
        => await Shell.Current.GoToAsync("//audio");

    [RelayCommand]
    public async Task GoToSettings()
        => await Shell.Current.GoToAsync("//settings");

    private void RefreshTexts()
    {
        THeader = _i18n.T("Map_Header");
        TPoiListTitle = _i18n.T("Map_PoiListTitle");
        TPoiListSub = _i18n.T("Map_PoiListSub");
        TLoading = _i18n.T("Map_Loading");
        TLoadingHint = _i18n.T("Map_LoadingHint");
        THotSeller = _i18n.T("Map_HotSeller");
        THasNarration = _i18n.T("Map_HasNarration");
        TReady = _i18n.T("Map_NarrationReady");
        TSyncing = _i18n.T("Map_Syncing");
        TNavHome = _i18n.T("Nav_Home");
        TNavMap = _i18n.T("Nav_Map");
        TNavAudio = _i18n.T("Nav_Audio");
        TNavSettings = _i18n.T("Nav_Settings");
    }
}
