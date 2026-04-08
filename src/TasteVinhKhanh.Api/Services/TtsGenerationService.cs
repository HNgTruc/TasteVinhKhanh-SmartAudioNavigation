using System.Diagnostics;

namespace TasteVinhKhanh.Api.Services;

/// <summary>
/// Dịch vụ TTS — chuyển text thành audio file.
/// Dùng Edge TTS (Python edge-tts) — miễn phí, chất lượng cao.
/// Chạy edge-tts như subprocess, không cần API key.
/// </summary>
public interface ITtsGenerationService
{
    Task<TtsResult> GenerateFromTextAsync(string text, string languageCode);
}

public class TtsResult
{
    public bool Success { get; set; }
    public byte[]? AudioBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ContentType { get; set; }
}

public class TtsGenerationService : ITtsGenerationService
{
    private readonly ILogger<TtsGenerationService> _log;
    private readonly string _pythonPath;

    // Voice map — dùng voice ổn định, có fallback
    private static readonly Dictionary<string, string[]> VoiceOptions = new()
    {
        ["vi"] = new[] { "vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural", "vi-VN-GiangNeural" },
        ["en"] = new[] { "en-US-JennyNeural", "en-US-AriaNeural", "en-US-GuyNeural" },
        ["zh"] = new[] { "zh-CN-XiaoxiaoNeural", "zh-CN-YunxiNeural", "zh-CN-YunyangNeural" },
        ["ko"] = new[] { "ko-KR-SunHiNeural", "ko-KR-JiMinNeural" },
        ["ja"] = new[] { "ja-JP-NanamiNeural", "ja-JP-KyotoNeural" }
    };

    public TtsGenerationService(ILogger<TtsGenerationService> log)
    {
        _log = log;
        _pythonPath = FindPython();
        _log.LogInformation("TTS Service init: python={PyPath}", _pythonPath);
    }

    private static string FindPython()
    {
        // Thứ tự ưu tiên: python.exe trực tiếp trên Windows, rồi python3
        var paths = new[]
        {
            @"C:\Python313\python.exe",
            @"C:\Python312\python.exe",
            @"C:\Python311\python.exe",
            @"C:\Python310\python.exe",
            @"C:\Python39\python.exe",
            "python",
            "python3"
        };
        foreach (var p in paths)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = p,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode == 0)
                    {
                        // Verify edge-tts is actually importable
                        var checkPsi = new ProcessStartInfo
                        {
                            FileName = p,
                            Arguments = "-c \"import edge_tts; print('OK')\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var check = Process.Start(checkPsi);
                        if (check != null)
                        {
                            check.WaitForExit(5000);
                            if (check.ExitCode == 0) return p;
                        }
                    }
                }
            }
            catch { /* skip */ }
        }
        return "python"; // fallback cuối
    }

    public async Task<TtsResult> GenerateFromTextAsync(string text, string languageCode)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TtsResult { Success = false, ErrorMessage = "Text is empty" };

        try
        {
            return await GenerateEdgeTtsAsync(text, languageCode);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "TTS generation failed for lang={Lang}", languageCode);
            return new TtsResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<TtsResult> GenerateEdgeTtsAsync(string text, string languageCode)
    {
        var voices = VoiceOptions.GetValueOrDefault(languageCode,
            new[] { "en-US-JennyNeural" });

        foreach (var voice in voices)
        {
            var result = await TryGenerateWithVoiceAsync(text, voice, languageCode);
            if (result.Success) return result;
            // Thử voice tiếp theo
            _log.LogWarning("Voice {Voice} failed, trying next...", voice);
        }

        return new TtsResult
        {
            Success = false,
            ErrorMessage = $"Không có voice nào hoạt động cho '{languageCode}'. " +
                "Vui lòng kiểm tra: pip install edge-tts && pip install aiohttp"
        };
    }

    private async Task<TtsResult> TryGenerateWithVoiceAsync(string text, string voice, string languageCode)
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.mp3");

        try
        {
            // Viết script vào temp file để tránh shell escaping hoàn toàn
            var scriptContent = $@"
import asyncio
import edge_tts
import sys

async def main():
    try:
        communicate = edge_tts.Communicate({EscapePythonStr(text)}, {EscapePythonStr(voice)})
        await communicate.save({EscapePythonStr(tmpFile)})
        print('SUCCESS')
    except Exception as e:
        print(f'ERROR: {{e}}', file=sys.stderr)
        sys.exit(1)

asyncio.run(main())
";
            var scriptFile = Path.Combine(Path.GetTempPath(), $"tts_script_{Guid.NewGuid()}.py");
            await File.WriteAllTextAsync(scriptFile, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{scriptFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _log.LogInformation("Running edge-tts: voice={Voice}, python={Py}", voice, _pythonPath);

            using var proc = Process.Start(psi);
            if (proc == null)
                return new TtsResult { Success = false, ErrorMessage = "Cannot start Python process" };

            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(new CancellationTokenSource(30000).Token); // 30s timeout

            // Cleanup script file
            try { File.Delete(scriptFile); } catch { /* ignore */ }

            if (proc.ExitCode == 0 && File.Exists(tmpFile))
            {
                var bytes = await File.ReadAllBytesAsync(tmpFile);
                try { File.Delete(tmpFile); } catch { /* ignore */ }
                _log.LogInformation("TTS generated: lang={Lang}, voice={Voice}, size={Size} bytes",
                    languageCode, voice, bytes.Length);
                return new TtsResult { Success = true, AudioBytes = bytes, ContentType = "audio/mpeg" };
            }

            // Extract error message
            var err = string.IsNullOrWhiteSpace(stderr) ? "Unknown error" : stderr.Trim();
            _log.LogWarning("edge-tts failed (exit={Exit}): {Error}", proc.ExitCode, err);
            return new TtsResult { Success = false, ErrorMessage = err };
        }
        catch (Exception ex)
        {
            // Cleanup on error
            try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { /* ignore */ }
            return new TtsResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Escape a string for embedding inside a Python string literal.
    /// Uses single-quoted string with backslash-escaped special chars.
    /// </summary>
    private static string EscapePythonStr(string s)
    {
        if (s == null) s = "";
        // Escape backslash, single-quote, and newlines
        return "'" + s
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            + "'";
    }
}
