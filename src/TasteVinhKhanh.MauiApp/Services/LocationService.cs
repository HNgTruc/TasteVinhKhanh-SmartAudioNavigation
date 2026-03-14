namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>Theo dõi GPS liên tục foreground + background</summary>
public class LocationService
{
    private CancellationTokenSource? _cts;

    public event Action<Location>? LocationUpdated;
    public Location? LastLocation { get; private set; }

    public async Task StartAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted) return;

        _cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                    var location = await Geolocation.GetLocationAsync(request, _cts.Token);

                    if (location != null)
                    {
                        LastLocation = location;
                        LocationUpdated?.Invoke(location);
                    }
                }
                catch (FeatureNotSupportedException) { break; }
                catch (PermissionException) { break; }
                catch { }

                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
            }
        }, _cts.Token);
    }

    public void Stop() => _cts?.Cancel();
}