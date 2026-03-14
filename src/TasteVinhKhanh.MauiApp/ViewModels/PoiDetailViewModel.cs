using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

[QueryProperty(nameof(PoiId), "poiId")]
public partial class PoiDetailViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;
    private readonly LocationService _location;

    [ObservableProperty] private int _poiId;
    [ObservableProperty] private LocalPoi? _poi;
    [ObservableProperty] private List<LocalAudioScript> _scripts = new();

    public PoiDetailViewModel(AppDatabase db, NarrationEngine narration, LocationService location)
    {
        _db = db;
        _narration = narration;
        _location = location;
    }

    partial void OnPoiIdChanged(int value) => _ = LoadAsync(value);

    async Task LoadAsync(int id)
    {
        Poi = await _db.GetPoiByIdAsync(id);
        if (Poi == null) return;

        // Lấy tất cả scripts của POI này
        var allScripts = new List<LocalAudioScript>();
        foreach (var lang in new[] { "vi", "en", "zh", "ko", "ja" })
        {
            var s = await _db.GetAudioScriptAsync(id, lang);
            if (s != null) allScripts.Add(s);
        }
        Scripts = allScripts;
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
}