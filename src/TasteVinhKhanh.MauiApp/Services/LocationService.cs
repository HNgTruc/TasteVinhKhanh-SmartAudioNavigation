namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Theo dõi GPS liên tục — foreground.
/// Background location (khi app không mở) cần cấu hình thêm Android Service
/// trong AndroidManifest.xml và MainActivity.
/// </summary>
public class LocationService
{
    private CancellationTokenSource? _cts;
    private readonly NotificationService _notif;
    private readonly object _runLock = new();

    public event Action<Location>? LocationUpdated;
    public Location? LastLocation { get; private set; }

    public LocationService(NotificationService notif)
    {
        _notif = notif;
    }

    public async Task StartAsync()
    {
        lock (_runLock)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                return;
        }

        // Yêu cầu quyền location
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            return;

        lock (_runLock)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                return;
            _cts = new CancellationTokenSource();
        }
        var cts = _cts;
        if (cts == null) return;

        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                    var location = await Geolocation.GetLocationAsync(request, cts.Token);

                    if (location != null)
                    {
                        LastLocation = location;
                        LocationUpdated?.Invoke(location);
                    }
                }
                catch (FeatureNotSupportedException) { break; }
                catch (PermissionException) { break; }
                catch { /* GPS không khả dụng */ }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token);
    }

    public void Stop()
    {
        lock (_runLock)
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
