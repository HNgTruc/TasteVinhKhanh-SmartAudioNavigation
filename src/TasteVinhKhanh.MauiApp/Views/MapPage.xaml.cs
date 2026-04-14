using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;
    private readonly Dictionary<int, Pin> _pinMap = new();
    private Microsoft.Maui.Controls.Maps.Map? _map;
    private string _lastPoiSignature = "";

    public MapPage(MapViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        Loaded += OnPageLoaded;

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_vm.Pois))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    AddPinsAndGeofencesToMap());
            }
        };

        _vm.LocationChanged += _ => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_vm.CurrentUserLocation != null)
                _vm.UpdateNearbyHighlight(_vm.CurrentUserLocation);
        });
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        _map = googleMap;

        if (_vm.Pois.Count > 0)
            AddPinsAndGeofencesToMap();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }

    private void AddPinsAndGeofencesToMap()
    {
        if (_map == null) return;

        var signature = string.Join('|', _vm.Pois
            .OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{p.UpdatedAt.Ticks}:{p.Latitude:F6}:{p.Longitude:F6}:{p.TriggerRadiusMeters:F1}"));
        if (signature == _lastPoiSignature) return;

        _pinMap.Clear();
        _map.Pins.Clear();
        _map.MapElements.Clear();

        if (_vm.Pois.Count == 0) return;

        var positions = new List<Location>();

        foreach (var poi in _vm.Pois)
        {
            var loc = new Location(poi.Latitude, poi.Longitude);
            positions.Add(loc);

            // ── PIN ──
            var pin = new Pin
            {
                Label = poi.Name,
                Address = $"{poi.TriggerRadiusMeters}m",
                Location = loc,
                Type = PinType.Place,
            };
            pin.MarkerClicked += OnPinMarkerClicked;
            _pinMap[poi.Id] = pin;
            _map.Pins.Add(pin);

            // ── VÒNG GEOFENCE (Circle) ──
            var circle = new Circle
            {
                Center = loc,
                Radius = Distance.FromMeters(poi.TriggerRadiusMeters),
                FillColor = GetGeofenceFillColor(poi.Priority),
                StrokeColor = GetGeofenceStrokeColor(poi.Priority),
                StrokeWidth = 2
            };
            _map.MapElements.Add(circle);
        }

        FitMapToPositions(positions);
        _lastPoiSignature = signature;
    }

    private void FitMapToPositions(List<Location> positions)
    {
        if (_map == null || positions.Count == 0) return;

        if (positions.Count == 1)
        {
            _map.MoveToRegion(MapSpan.FromCenterAndRadius(
                positions[0], Distance.FromMeters(300)));
            return;
        }

        double minLat = positions.Min(p => p.Latitude);
        double maxLat = positions.Max(p => p.Latitude);
        double minLon = positions.Min(p => p.Longitude);
        double maxLon = positions.Max(p => p.Longitude);

        double latPad = (maxLat - minLat) * 0.4;
        double lonPad = (maxLon - minLon) * 0.4;

        var center = new Location(
            (minLat + maxLat) / 2,
            (minLon + maxLon) / 2);

        double radiusM = Math.Max(
            LatLongToMeters(maxLat - minLat + latPad, 0),
            LatLongToMeters(0, maxLon - minLon + lonPad));

        _map.MoveToRegion(MapSpan.FromCenterAndRadius(
            center, Distance.FromMeters(radiusM)));
    }

    private static double LatLongToMeters(double dLat, double dLon)
    {
        const double earthRadiusM = 6371000;
        double latM = dLat * Math.PI / 180 * earthRadiusM;
        double lonM = dLon * Math.PI / 180 * earthRadiusM
                       * Math.Cos(10.757 * Math.PI / 180);
        return Math.Max(Math.Abs(latM), Math.Abs(lonM));
    }

    private static Color GetGeofenceFillColor(int priority) => priority switch
    {
        >= 5 => Color.FromArgb("#33FF6B35"),
        >= 4 => Color.FromArgb("#28FF8C42"),
        >= 3 => Color.FromArgb("#1AFF6B35"),
        _    => Color.FromArgb("#15FF6B35"),
    };

    private static Color GetGeofenceStrokeColor(int priority) => priority switch
    {
        >= 5 => Color.FromArgb("#AAFF6B35"),
        >= 4 => Color.FromArgb("#88FF8C42"),
        >= 3 => Color.FromArgb("#66FF6B35"),
        _    => Color.FromArgb("#44FF6B35"),
    };

    private async void OnPinMarkerClicked(object? sender, PinClickedEventArgs e)
    {
        if (sender is not Pin pin) return;

        var poi = _vm.Pois.FirstOrDefault(p =>
            Math.Abs(p.Latitude - pin.Location.Latitude) < 0.0001 &&
            Math.Abs(p.Longitude - pin.Location.Longitude) < 0.0001);

        if (poi != null)
            await _vm.GoToDetail(poi.Id);
    }

    private async void OnPoiCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is LocalPoi poi)
            await _vm.GoToDetail(poi.Id);
    }

    private void FitAllPois_Click(object? sender, EventArgs e)
    {
        if (_vm.Pois.Count == 0 || _map == null) return;
        var positions = _vm.Pois
            .Select(p => new Location(p.Latitude, p.Longitude))
            .ToList();
        FitMapToPositions(positions);
    }

    private void RecenterToUser_Click(object? sender, EventArgs e)
    {
        if (_map == null) return;
        var userLoc = _vm.CurrentUserLocation;
        if (userLoc != null)
        {
            _map.MoveToRegion(MapSpan.FromCenterAndRadius(
                userLoc, Distance.FromMeters(200)));
        }
    }
}
