using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.Http.Json;
using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class ToursViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly LocalizationService _i18n;

    [ObservableProperty] private List<TourListItemDto> _tours = new();
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _hasStatus = false;
    [ObservableProperty] private bool _isInitialized = false;
    [ObservableProperty] private string _tHeader = "";
    [ObservableProperty] private string _tSubtitle = "";
    [ObservableProperty] private string _tEmpty = "";

    public ToursViewModel(HttpClient http, LocalizationService i18n)
    {
        _http = http;
        _i18n = i18n;
        RefreshTexts();
        _i18n.LanguageChanged += RefreshTexts;
    }

    [RelayCommand]
    public async Task InitAsync()
    {
        if (IsInitialized) return;

        IsLoading = true;
        StatusMessage = "";
        HasStatus = false;
        try
        {
            var resp = await _http.GetFromJsonAsync<TourPagedDto>("api/tour?page=1&pageSize=30&includeInactive=false");
            Tours = resp?.Items ?? new List<TourListItemDto>();
            if (Tours.Count == 0)
            {
                StatusMessage = TEmpty;
                HasStatus = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Tours = new List<TourListItemDto>();
            HasStatus = true;
        }
        finally
        {
            IsLoading = false;
            IsInitialized = true;
        }
    }

    [RelayCommand]
    public async Task GoBack()
        => await Shell.Current.GoToAsync("//main");

    [RelayCommand]
    public async Task SelectTour(int tourId)
    {
        try
        {
            var detail = await _http.GetFromJsonAsync<TourDetailDto>($"api/tour/{tourId}");
            if (detail == null || detail.Pois.Count == 0)
            {
                StatusMessage = TEmpty;
                HasStatus = true;
                return;
            }

            var firstPoiId = detail.Pois.OrderBy(p => p.StopOrder).First().PoiId;
            await Shell.Current.GoToAsync($"PoiDetailPage?poiId={firstPoiId}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            HasStatus = true;
        }
    }

    private void RefreshTexts()
    {
        THeader = _i18n.T("Tour_Header");
        TSubtitle = _i18n.T("Tour_Subtitle");
        TEmpty = _i18n.T("Tour_Empty");
    }
}
