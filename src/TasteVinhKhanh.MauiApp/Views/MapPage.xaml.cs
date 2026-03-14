using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;

    public MapPage(MapViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // Lắng nghe khi POI load xong thì thêm pins lên map
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_vm.Pois))
                AddPinsToMap();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();

        // Di chuyển map về khu vực phố Vĩnh Khánh
        googleMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(10.7570, 106.7000),
            Distance.FromMeters(500)));
    }

    private void AddPinsToMap()
    {
        googleMap.Pins.Clear();
        foreach (var poi in _vm.Pois)
        {
            var pin = new Pin
            {
                Label = poi.Name,
                Address = poi.ShortDescription,
                Location = new Location(poi.Latitude, poi.Longitude),
                Type = PinType.Place
            };
            googleMap.Pins.Add(pin);
        }
    }
}