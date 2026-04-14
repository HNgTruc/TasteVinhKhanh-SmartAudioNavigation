using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Converters;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly LocalizationService _i18n;
    private HashSet<int> _favoriteIds;

    [ObservableProperty] private List<LocalPoi> _favoritePois = new();
    [ObservableProperty] private string _favoriteCountLabel = "";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasFavorites = false;

    // ── Bindable translated strings ──
    [ObservableProperty] private string _tHeader = "";
    [ObservableProperty] private string _tTitle = "";
    [ObservableProperty] private string _tSubtitle = "";
    [ObservableProperty] private string _tEmpty = "";
    [ObservableProperty] private string _tEmptyHint = "";
    [ObservableProperty] private string _tNavHome = "";
    [ObservableProperty] private string _tNavMap = "";
    [ObservableProperty] private string _tNavAudio = "";
    [ObservableProperty] private string _tNavSettings = "";

    public FavoritesViewModel(AppDatabase db, LocalizationService i18n)
    {
        _db = db;
        _i18n = i18n;

        // Load favorite IDs from Preferences
        var stored = Preferences.Get(FavKey, "");
        _favoriteIds = string.IsNullOrEmpty(stored)
            ? new HashSet<int>()
            : new HashSet<int>(stored.Split(',').Select(int.Parse));

        // Wire up the static converter so heart icons render correctly
        FavoriteIconConverter.IsFavoriteFunc = IsFavorite;

        _i18n.LanguageChanged += RefreshTexts;
        RefreshTexts();
    }

    private static readonly string FavKey = "favorite_pois";

    // NOTE: _favoriteIds is intentionally non-readonly so InitAsync can reload from Preferences

    public bool IsFavorite(int poiId) => _favoriteIds.Contains(poiId);

    [RelayCommand]
    public async Task InitAsync()
    {
        IsLoading = true;

        // Reload favorite IDs (user may have toggled from MapPage)
        var stored = Preferences.Get(FavKey, "");
        _favoriteIds = string.IsNullOrEmpty(stored)
            ? new HashSet<int>()
            : new HashSet<int>(stored.Split(',').Select(int.Parse));

        // Reload converter func
        FavoriteIconConverter.IsFavoriteFunc = IsFavorite;

        if (_favoriteIds.Count == 0)
        {
            FavoritePois = new List<LocalPoi>();
            FavoriteCountLabel = "0 quán";
            HasFavorites = false;
            IsLoading = false;
            return;
        }

        // Load POI details from SQLite
        var allPois = await _db.GetAllPoisAsync();
        FavoritePois = allPois.Where(p => _favoriteIds.Contains(p.Id)).ToList();
        FavoriteCountLabel = $"{FavoritePois.Count} quán";
        HasFavorites = FavoritePois.Count > 0;

        IsLoading = false;
    }

    /// <summary>
    /// Remove a POI from favorites (called from the heart button in this list).
    /// </summary>
    [RelayCommand]
    public void ToggleFavorite(int poiId)
    {
        if (_favoriteIds.Contains(poiId))
            _favoriteIds.Remove(poiId);
        else
            _favoriteIds.Add(poiId);

        Preferences.Set(FavKey, string.Join(",", _favoriteIds));

        // Update UI list immediately
        FavoritePois = FavoritePois.Where(p => p.Id != poiId).ToList();
        FavoriteCountLabel = $"{FavoritePois.Count} quán";
        HasFavorites = FavoritePois.Count > 0;
    }

    [RelayCommand]
    public async Task GoToDetail(int poiId)
        => await Shell.Current.GoToAsync($"PoiDetailPage?poiId={poiId}");

    [RelayCommand]
    public async Task GoToHome() => await Shell.Current.GoToAsync("//main");

    [RelayCommand]
    public async Task GoToMap() => await Shell.Current.GoToAsync("//map");

    [RelayCommand]
    public async Task GoToAudio() => await Shell.Current.GoToAsync("//audio");

    [RelayCommand]
    public async Task GoToSettings() => await Shell.Current.GoToAsync("//settings");

    private void RefreshTexts()
    {
        THeader = _i18n.T("Fav_Header");
        TTitle = _i18n.T("Fav_Title");
        TSubtitle = _i18n.T("Fav_Subtitle");
        TEmpty = _i18n.T("Fav_Empty");
        TEmptyHint = _i18n.T("Fav_EmptyHint");
        TNavHome = _i18n.T("Nav_Home");
        TNavMap = _i18n.T("Nav_Map");
        TNavAudio = _i18n.T("Nav_Audio");
        TNavSettings = _i18n.T("Nav_Settings");
    }
}
