using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
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

[QueryProperty(nameof(PoiId), "poiId")]
public partial class PoiDetailViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;
    private readonly LocationService _location;

    [ObservableProperty] private int _poiId;
    [ObservableProperty] private LocalPoi? _poi;
    [ObservableProperty] private List<LocalAudioScript> _scripts = new();
    [ObservableProperty] private List<LocalRestaurantImage> _images = new();
    [ObservableProperty] private List<ImageDotItem> _dots = new();
    [ObservableProperty] private int _currentImageIndex = 0;
    [ObservableProperty] private int _selectedTabIndex = 0;
    [ObservableProperty] private string? _debugInfo;

    public int ImagesCount => Images.Count;

    public LocalRestaurantImage? CurrentImage
        => Images.Count > 0 && CurrentImageIndex >= 0 && CurrentImageIndex < Images.Count
            ? Images[CurrentImageIndex] : null;

    // Tab highlight
    public bool IsTabLocation  => SelectedTabIndex == 0;
    public bool IsTabNarration => SelectedTabIndex == 1;
    public bool IsTabFavorite  => SelectedTabIndex == 2;

    public PoiDetailViewModel(AppDatabase db, NarrationEngine narration, LocationService location)
    {
        _db = db;
        _narration = narration;
        _location = location;
    }

    partial void OnPoiIdChanged(int value) => _ = LoadAsync(value);

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
        if (Poi == null || _location.LastLocation == null) return;
        await _narration.PlayAsync(Poi, 0, _location.LastLocation, "manual");
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
    public async Task ToggleFavoriteAsync()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    public void GoToHome() => Shell.Current.GoToAsync("//main");

    [RelayCommand]
    public void GoToMap() => Shell.Current.GoToAsync("//map");

    [RelayCommand]
    public void GoToAudio() => Shell.Current.GoToAsync("//audio");

    [RelayCommand]
    public void GoToSettings() => Shell.Current.GoToAsync("//settings");
}