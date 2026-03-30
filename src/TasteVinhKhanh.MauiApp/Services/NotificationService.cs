using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;


namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Gửi local notification khi người dùng đến gần 1 POI.
/// </summary>
public class NotificationService
{
    private const int PoiNotificationId = 1000;

    public async Task ShowPoiNotificationAsync(string poiName, double distanceMeters)
    {
        try
        {
            var notif = new NotificationRequest
            {
                Title = "🍜 TasteVinhKhanh",
                Description = $"📍 {poiName} — {distanceMeters:F0}m. Nhấn để nghe thuyết minh.",
                NotificationId = PoiNotificationId,
                Android = new AndroidOptions
                {
                    ChannelId = "poi_channel",
                    Priority = AndroidPriority.High
                }
            };

            await LocalNotificationCenter.Current.Show(notif);
        }
        catch
        {
            // Notification không khả dụng trên thiết bị này
        }
    }
}
