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

    [ObservableProperty] private List<LocalPoi> _pois = new();
    [ObservableProperty] private LocalPoi? _nearestPoi;
    [ObservableProperty] private LocalPoi? _selectedPoi;
    [ObservableProperty] private string _statusMessage = "Đang khởi động...";
    [ObservableProperty] private string _syncDetail = "";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _currentLanguage = "vi";
    [ObservableProperty] private string _poiCountLabel = "0 quán";
    [ObservableProperty] private Location? _currentUserLocation;
    [ObservableProperty] private int? _nearestPoiId;

    // Favorites stored as comma-separated IDs in Preferences
    private static readonly string FavKey = "favorite_pois";
    private HashSet<int> _favoriteIds = new();

    /// <summary>
    /// Sự kiện bắn khi vị trí người dùng thay đổi — dùng bởi MapPage.
    /// </summary>
    public event Action<Location>? LocationChanged;

    public MapViewModel(AppDatabase db, LocationService location,
        GeofenceEngine geofence, SyncService sync, NarrationEngine narration)
    {
        _db = db;
        _location = location;
        _geofence = geofence;
        _sync = sync;
        _narration = narration;

        // Load favorites
        LoadFavorites();
        FavoriteIconConverter.IsFavoriteFunc = IsFavorite;

        // Lắng nghe GPS update
        _location.LocationUpdated += OnLocationUpdated;

        // Lắng nghe khi bắt đầu phát
        _narration.NarrationStarted += name =>
            StatusMessage = $"Đang phát: {name}";

        // Lắng nghe khi phát xong
        _narration.NarrationFinished += () =>
            StatusMessage = "Sẵn sàng";

        // Lắng nghe khi POI được kích hoạt (gần)
        _geofence.PoiTriggered += (poi, dist) =>
        {
            NearestPoi = poi;
            NearestPoiId = poi.Id;
            StatusMessage = $"Gần: {poi.Name} ({dist:F0}m)";
        };
    }

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
        StatusMessage = "Đang đồng bộ dữ liệu...";
        SyncDetail = "";

        await _db.InitAsync();

        // Sync từ server (nếu API chạy)
        var result = await _sync.SyncPoisAsync();

        // Hiện chi tiết sync — rất quan trọng để debug
        if (!result.Success)
        {
            SyncDetail = $"⚠️ Sync thất bại: {result.Message}";
            StatusMessage = "⚠️ Không sync được";
        }
        else if (result.FromCache)
        {
            SyncDetail = $"📴 {result.Message}";
            StatusMessage = $"📴 Offline: {result.UpdatedCount} điểm";
        }
        else
        {
            SyncDetail = $"✅ {result.Message}";
            StatusMessage = result.UpdatedCount > 0
                ? $"Đã tải {result.UpdatedCount} điểm"
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
            StatusMessage = "❌ Không có điểm nào";
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
        LocationChanged?.Invoke(location); // Bắn sự kiện cho MapPage
        await _geofence.CheckLocationAsync(location);
    }

    /// <summary>
    /// Highlight POI gần nhất (gọi từ MapPage khi có location update).
    /// </summary>
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
        }
    }

    /// <summary>
    /// Khoảng cách Haversine (mét).
    /// </summary>
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
        _narration.CurrentLanguage = lang;
    }

    [RelayCommand]
    public async Task GoToDetail(int poiId)
    {
        await Shell.Current.GoToAsync($"PoiDetailPage?poiId={poiId}");
    }

    [RelayCommand]
    public async Task GoToHome()
        => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    public async Task GoToAudio()
        => await Shell.Current.GoToAsync("//AudioPage");

    [RelayCommand]
    public async Task GoToSettings()
        => await Shell.Current.GoToAsync("//SettingsPage");
}
