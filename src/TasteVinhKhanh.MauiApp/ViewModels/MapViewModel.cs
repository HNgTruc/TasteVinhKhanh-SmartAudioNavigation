using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private string _statusMessage = "Đang khởi động...";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _currentLanguage = "vi";

    public MapViewModel(AppDatabase db, LocationService location,
        GeofenceEngine geofence, SyncService sync, NarrationEngine narration)
    {
        _db = db;
        _location = location;
        _geofence = geofence;
        _sync = sync;
        _narration = narration;

        // Lắng nghe GPS update
        _location.LocationUpdated += OnLocationUpdated;

        // Lắng nghe khi bắt đầu phát
        _narration.NarrationStarted += name =>
            StatusMessage = $"🔊 Đang phát: {name}";

        // Lắng nghe khi POI được kích hoạt
        _geofence.PoiTriggered += (poi, dist) =>
        {
            NearestPoi = poi;
            StatusMessage = $"📍 Gần: {poi.Name} ({dist:F0}m)";
        };
    }

    [RelayCommand]
    public async Task InitAsync()
    {
        IsLoading = true;
        StatusMessage = "Đang đồng bộ dữ liệu...";

        await _db.InitAsync();

        // Sync từ server
        var result = await _sync.SyncPoisAsync();
        StatusMessage = result.Message;

        // Load POI từ SQLite
        Pois = await _db.GetAllPoisAsync();

        // Bắt đầu theo dõi GPS
        await _location.StartAsync();
        StatusMessage = "✅ Sẵn sàng";

        IsLoading = false;

        // Upload log cũ nếu có mạng
        _ = _sync.UploadPendingLogsAsync();
    }

    private async void OnLocationUpdated(Location location)
    {
        await _geofence.CheckLocationAsync(location);
    }

    [RelayCommand]
    public void ChangeLanguage(string lang)
    {
        CurrentLanguage = lang;
        _narration.CurrentLanguage = lang;
    }

    private string shortDescription;

    public string ShortDescription { get => shortDescription; set => SetProperty(ref shortDescription, value); }

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