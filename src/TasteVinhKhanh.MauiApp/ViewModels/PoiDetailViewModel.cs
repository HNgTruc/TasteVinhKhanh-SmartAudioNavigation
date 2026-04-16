using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public class ImageDotItem
{
    public int Index { get; set; }
    public bool IsActive { get; set; }
    public Color DotColor => IsActive
        ? Color.FromRgb(255, 107, 53)   // #FF6B35 cam sáng
        : Color.FromRgb(68, 68, 68);    // #444444 xám
}

// Tên hiển thị cho từng ngôn ngữ
public static class LanguageNames
{
    private static readonly Dictionary<string, string[]> _names = new()
    {
        ["vi"] = new[] { "Tiếng Việt", "Tiếng Việt", "Tiếng Việt", "Tiếng Việt", "Tiếng Việt" },
        ["en"] = new[] { "English", "English", "English", "English", "English" },
        ["zh"] = new[] { "中文", "中文", "中文", "中文", "中文" },
        ["ko"] = new[] { "한국어", "한국어", "한국어", "한국어", "한국어" },
        ["ja"] = new[] { "日本語", "日本語", "日本語", "日本語", "日本語" },
    };
    public static string Get(string langCode, string uiLang)
    {
        var uiIdx = uiLang switch { "vi" => 0, "en" => 1, "zh" => 2, "ko" => 3, "ja" => 4, _ => 0 };
        return _names.GetValueOrDefault(langCode, _names["vi"])[uiIdx];
    }
}

[QueryProperty(nameof(PoiId), "poiId")]
[QueryProperty(nameof(TourPoiIds), "tourPoiIds")]
[QueryProperty(nameof(TourIndex), "tourIndex")]
public partial class PoiDetailViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;
    private readonly LocationService _location;
    private readonly LocalizationService _i18n;
    private readonly GeofenceEngine _geofence;

    [ObservableProperty] private int _poiId;
    [ObservableProperty] private string? _tourPoiIds;
    [ObservableProperty] private int _tourIndex;
    [ObservableProperty] private LocalPoi? _poi;
    [ObservableProperty] private List<LocalAudioScript> _scripts = new();
    [ObservableProperty] private List<LocalRestaurantImage> _images = new();
    [ObservableProperty] private List<ImageDotItem> _dots = new();
    [ObservableProperty] private int _currentImageIndex = 0;
    [ObservableProperty] private int _selectedTabIndex = 0;
    [ObservableProperty] private string? _debugInfo;

    // ── Translated tab/button labels ──
    [ObservableProperty] private string _tLocation = "";
    [ObservableProperty] private string _tNarration = "";
    [ObservableProperty] private string _tFavorite = "";
    [ObservableProperty] private string _tLocationTitle = "";
    [ObservableProperty] private string _tOpenMap = "";
    [ObservableProperty] private string _tPlayNarration = "";
    [ObservableProperty] private string _tNarrationTitle = "";
    [ObservableProperty] private string _tAvailableLangs = "";
    [ObservableProperty] private string _tFavoriteTitle = "";
    [ObservableProperty] private string _tFavoriteAdd = "";
    [ObservableProperty] private string _tLangLabel = "";
    [ObservableProperty] private string _tNavHome = "";
    [ObservableProperty] private string _tNavMap = "";
    [ObservableProperty] private string _tNavAudio = "";
    [ObservableProperty] private string _tNavFavorites = "";
    [ObservableProperty] private string _tNavSettings = "";

    // ── Language display ──
    [ObservableProperty] private string _currentLangDisplay = "Tiếng Việt";
    [ObservableProperty] private string _currentLangCode = "vi";
    [ObservableProperty] private string _currentAudioLangDisplay = "Tiếng Việt";
    // ── Playing state ──
    [ObservableProperty] private bool _isPlayingNarration = false;
    [ObservableProperty] private string? _playingLangCode;
    [ObservableProperty] private bool _isTourMode = false;
    [ObservableProperty] private bool _hasPreviousTourStop = false;
    [ObservableProperty] private bool _hasNextTourStop = false;
    [ObservableProperty] private string _tourProgressText = "";

    public int ImagesCount => Images.Count;

    public LocalRestaurantImage? CurrentImage
        => Images.Count > 0 && CurrentImageIndex >= 0 && CurrentImageIndex < Images.Count
            ? Images[CurrentImageIndex] : null;

    // Tab highlight
    public bool IsTabLocation  => SelectedTabIndex == 0;
    public bool IsTabNarration => SelectedTabIndex == 1;
    public bool IsTabFavorite  => SelectedTabIndex == 2;

    public PoiDetailViewModel(AppDatabase db, NarrationEngine narration,
        LocationService location, LocalizationService i18n, GeofenceEngine geofence)
    {
        _db = db;
        _narration = narration;
        _location = location;
        _i18n = i18n;
        _geofence = geofence;

        // Cập nhật hiển thị ngôn ngữ khi audio bắt đầu phát
        _narration.NarrationStartedWithLang += langCode => {
            IsPlayingNarration = true;
            PlayingLangCode = langCode;
            CurrentLangCode = langCode;
            CurrentLangDisplay = LanguageNames.Get(langCode, _i18n.CurrentLanguage);
            OnPropertyChanged(nameof(CurrentLangDisplay));
            OnPropertyChanged(nameof(IsPlayingNarration));
            OnPropertyChanged(nameof(PlayingLangCode));
            OnPropertyChanged(nameof(CurrentLangCode));
        };

        _narration.NarrationFinished += () => {
            IsPlayingNarration = false;
            PlayingLangCode = null;
            OnPropertyChanged(nameof(IsPlayingNarration));
            OnPropertyChanged(nameof(PlayingLangCode));
        };

        // Cập nhật hiển thị khi ngôn ngữ thay đổi ở Settings
        _i18n.LanguageChanged += () => {
            CurrentLangCode = _i18n.CurrentLanguage;
            CurrentLangDisplay = LanguageNames.Get(_i18n.CurrentLanguage, _i18n.CurrentLanguage);
            RefreshTexts();
            OnPropertyChanged(nameof(CurrentLangDisplay));
            OnPropertyChanged(nameof(CurrentLangCode));
        };

        RefreshLangDisplay();
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        TLocation = _i18n.T("Poi_Location");
        TNarration = _i18n.T("Poi_Narration");
        TFavorite = _i18n.T("Poi_Favorite");
        TLocationTitle = _i18n.T("Poi_LocationTitle");
        TOpenMap = _i18n.T("Poi_OpenMap");
        TPlayNarration = _i18n.T("Poi_PlayNarration");
        TNarrationTitle = _i18n.T("Poi_NarrationTitle");
        TAvailableLangs = _i18n.T("Poi_AvailableLangs");
        TFavoriteTitle = _i18n.T("Poi_FavoriteTitle");
        TFavoriteAdd = _i18n.T("Poi_FavoriteAdd");
        TLangLabel = _i18n.T("Poi_LangLabel");
        TNavHome = _i18n.T("Nav_Home");
        TNavMap = _i18n.T("Nav_Map");
        TNavAudio = _i18n.T("Nav_Audio");
        TNavFavorites = _i18n.T("Nav_Favorites");
        TNavSettings = _i18n.T("Nav_Settings");
    }

    private void RefreshLangDisplay()
    {
        CurrentLangCode = _i18n.CurrentLanguage;
        CurrentLangDisplay = LanguageNames.Get(CurrentLangCode, CurrentLangCode);
        CurrentAudioLangDisplay = LanguageNames.Get(CurrentLangCode, CurrentLangCode);
    }

    /// <summary>
    /// Đổi ngôn ngữ AUDIO (cho chip trong POI tab).
    /// KHÔNG thay đổi ngôn ngữ app.
    /// </summary>
    public void SafeSetAudioLang(string langCode)
    {
        CurrentLangCode = langCode;
        CurrentAudioLangDisplay = LanguageNames.Get(langCode, _i18n.CurrentLanguage);
        OnPropertyChanged(nameof(CurrentLangCode));
        OnPropertyChanged(nameof(CurrentAudioLangDisplay));
    }

    partial void OnCurrentLangCodeChanged(string value)
        => CurrentLangDisplay = LanguageNames.Get(value, _i18n.CurrentLanguage);

    partial void OnPoiIdChanged(int value) => _ = LoadAsync(value);

    partial void OnTourPoiIdsChanged(string? value)
    {
        RecomputeTourState();
    }

    partial void OnTourIndexChanged(int value)
    {
        RecomputeTourState();
    }

    partial void OnCurrentImageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentImage));
        RefreshDots();
    }

    private void RefreshDots()
    {
        if (Images == null || Images.Count == 0)
        {
            Dots = new List<ImageDotItem>();
            return;
        }
        Dots = Enumerable.Range(0, Images.Count)
            .Select(i => new ImageDotItem { Index = i, IsActive = i == CurrentImageIndex })
            .ToList();
    }

    private List<int> ParseTourPoiIds()
    {
        if (string.IsNullOrWhiteSpace(TourPoiIds)) return new List<int>();

        return TourPoiIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    private void RecomputeTourState()
    {
        var ids = ParseTourPoiIds();
        var inRange = ids.Count > 0 && TourIndex >= 0 && TourIndex < ids.Count;

        IsTourMode = inRange;
        HasPreviousTourStop = inRange && TourIndex > 0;
        HasNextTourStop = inRange && TourIndex < ids.Count - 1;
        TourProgressText = inRange ? $"Điểm dừng {TourIndex + 1}/{ids.Count}" : string.Empty;

        OnPropertyChanged(nameof(IsTourMode));
        OnPropertyChanged(nameof(HasPreviousTourStop));
        OnPropertyChanged(nameof(HasNextTourStop));
        OnPropertyChanged(nameof(TourProgressText));
    }

    async Task LoadAsync(int id)
    {
        try
        {
            Poi = await _db.GetPoiByIdAsync(id);
            if (Poi == null) return;

            var allScripts = new List<LocalAudioScript>();
            foreach (var lang in new[] { "vi", "en", "zh", "ko", "ja" })
            {
                var s = await _db.GetAudioScriptAsync(id, lang);
                if (s != null) allScripts.Add(s);
            }
            Scripts = allScripts;

            var imgs = await _db.GetImagesForPoiAsync(id);
            Images = imgs;
            CurrentImageIndex = 0;
            RefreshDots();
            OnPropertyChanged(nameof(ImagesCount));
            OnPropertyChanged(nameof(CurrentImage));
            DebugInfo = $"POI#{id}: {imgs.Count} ảnh gallery";
        }
        catch (Exception ex)
        {
            DebugInfo = $"Lỗi: {ex.Message}";
        }
    }

    [RelayCommand]
    public void PreviousImage()
    {
        if (Images.Count == 0) return;
        CurrentImageIndex = (CurrentImageIndex - 1 + Images.Count) % Images.Count;
    }

    [RelayCommand]
    public void NextImage()
    {
        if (Images.Count == 0) return;
        CurrentImageIndex = (CurrentImageIndex + 1) % Images.Count;
    }

    [RelayCommand]
    public async Task PlayNarrationAsync()
    {
        if (Poi == null) return;

        // Dùng vị trí thực nếu có, không thì dùng tọa độ mặc định TP.HCM
        var loc = _location.LastLocation
            ?? new Location(10.7629, 106.6604); // Q4, TP.HCM (gần phố Vĩnh Khánh)

        await _narration.PlayAsync(Poi, 0, loc, "manual");
    }

    [RelayCommand]
    public void OpenMap()
    {
        if (Poi?.MapUrl != null)
            _ = Launcher.OpenAsync(Poi.MapUrl);
    }

    [RelayCommand]
    public void SelectTab(int index)
    {
        SelectedTabIndex = index;
    }

    [RelayCommand]
    public void ToggleFavorite()
    {
        if (Poi == null) return;
        var key = "favorite_pois";
        var stored = Preferences.Get(key, "");
        var ids = string.IsNullOrEmpty(stored)
            ? new HashSet<int>()
            : new HashSet<int>(stored.Split(',').Select(int.Parse));

        if (ids.Contains(Poi.Id)) ids.Remove(Poi.Id);
        else ids.Add(Poi.Id);

        Preferences.Set(key, string.Join(",", ids));

        // Notify FavoriteIconConverter (static) so MapPage also refreshes
        AppShell.NotifyFavoriteChanged();

        OnPropertyChanged(nameof(IsFavoritePoi));
        OnPropertyChanged(nameof(IsFavoriteActive));
    }

    /// <summary>True = đang trong danh sách yêu thích. Dùng cho binding trên trang POI Detail.</summary>
    public bool IsFavoritePoi
    {
        get
        {
            var key = "favorite_pois";
            var stored = Preferences.Get(key, "");
            if (string.IsNullOrEmpty(stored)) return false;
            var ids = new HashSet<int>(stored.Split(',').Select(int.Parse));
            return Poi != null && ids.Contains(Poi.Id);
        }
    }

    /// <summary>True khi POI đang được yêu thích — dùng để bật hiệu ứng cam trên tab Yêu thích.</summary>
    public bool IsFavoriteActive => IsFavoritePoi;

    [RelayCommand]
    public void GoToHome() => Shell.Current.GoToAsync("//main");

    [RelayCommand]
    public void GoToMap() => Shell.Current.GoToAsync("//map");

    [RelayCommand]
    public void GoToAudio() => Shell.Current.GoToAsync("//audio");

    [RelayCommand]
    public void GoToSettings() => Shell.Current.GoToAsync("//settings");

    [RelayCommand]
    public void GoToFavorites() => Shell.Current.GoToAsync("//favorites");

    [RelayCommand]
    public async Task GoToPreviousTourStopAsync()
    {
        var ids = ParseTourPoiIds();
        if (!IsTourMode || TourIndex <= 0 || TourIndex >= ids.Count) return;

        var nextIndex = TourIndex - 1;
        var nextPoiId = ids[nextIndex];
        var poiIdsCsv = string.Join(",", ids);
        await Shell.Current.GoToAsync(
            $"PoiDetailPage?poiId={nextPoiId}&tourPoiIds={System.Uri.EscapeDataString(poiIdsCsv)}&tourIndex={nextIndex}");
    }

    [RelayCommand]
    public async Task GoToNextTourStopAsync()
    {
        var ids = ParseTourPoiIds();
        if (!IsTourMode || TourIndex < 0 || TourIndex >= ids.Count - 1) return;

        var nextIndex = TourIndex + 1;
        var nextPoiId = ids[nextIndex];
        var poiIdsCsv = string.Join(",", ids);
        await Shell.Current.GoToAsync(
            $"PoiDetailPage?poiId={nextPoiId}&tourPoiIds={System.Uri.EscapeDataString(poiIdsCsv)}&tourIndex={nextIndex}");
    }

    partial void OnIsPlayingNarrationChanged(bool value)
    {
        _geofence.SetGeofenceBlocked(value);
    }
}
