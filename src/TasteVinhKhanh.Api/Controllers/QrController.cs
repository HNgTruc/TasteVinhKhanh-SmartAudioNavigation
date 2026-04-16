using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TasteVinhKhanh.Api.Controllers;

[ApiController]
[Route("qr")]
public class QrController : ControllerBase
{
    [HttpGet("open")]
    [AllowAnonymous]
    public IActionResult Open(
        [FromQuery] string app = "tastevinhkhanh://open",
        [FromQuery] string apk = "/qr/app-latest.apk")
    {
        var appLink = string.IsNullOrWhiteSpace(app) ? "tastevinhkhanh://open" : app.Trim();
        var apkLink = string.IsNullOrWhiteSpace(apk) ? "/qr/app-latest.apk" : apk.Trim();

        var html = $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>Open TasteVinhKhanh</title>
  <style>
    body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:#0f1115;color:#fff;display:flex;min-height:100vh;align-items:center;justify-content:center}
    .card{width:min(92vw,460px);background:#1a1f29;border:1px solid #2b3342;border-radius:16px;padding:24px;box-shadow:0 12px 40px rgba(0,0,0,.35)}
    h1{margin:0 0 8px;font-size:24px}
    p{margin:0 0 16px;color:#c3ccdc;line-height:1.45}
    .hint{font-size:12px;color:#9ca3af;margin-top:10px}
    .btn{display:block;text-decoration:none;text-align:center;padding:12px 14px;border-radius:12px;font-weight:700;margin-top:10px}
    .btn-open{background:#ff6b35;color:#fff}
    .btn-apk{background:#2b3342;color:#fff}
  </style>
</head>
<body>
  <div class="card">
    <h1>Open TasteVinhKhanh</h1>
    <p>If the app is installed, tap "Open App". If nothing happens, tap "Download APK".</p>
    <a class="btn btn-open" id="openBtn" href="{{appLink}}">Open App</a>
    <a class="btn btn-apk" href="{{apkLink}}">Download APK</a>
    <div class="hint">Tip: On iPhone, automatic deep-link redirects are often blocked. Manual tap is more reliable.</div>
  </div>
  <script>
    const appLink = {{System.Text.Json.JsonSerializer.Serialize(appLink)}};
    // No auto redirect on load. Keep landing stable across iOS browsers.
    // User explicitly taps Open App to trigger deep link.
  </script>
</body>
</html>
""";

        return Content(html, "text/html");
    }
}
