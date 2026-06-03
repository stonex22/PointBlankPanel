using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PointBlankPanel.Services;

public class UpdateInfo
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("url")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("changelog")] public string Changelog { get; set; } = "";
}

public static class AutoUpdater
{
    public static string UpdateCheckUrl { get; set; } = "";
    public static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateCheckUrl)) return null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync(UpdateCheckUrl).ConfigureAwait(false);
            return JsonSerializer.Deserialize<UpdateInfo>(json);
        }
        catch { return null; }
    }

    public static async Task<string> DownloadAndUpdateAsync(UpdateInfo info)
    {
        try
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "PBUpdater");
            Directory.CreateDirectory(tmpDir);
            var tmpFile = Path.Combine(tmpDir, "update.exe");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var data = await http.GetByteArrayAsync(info.DownloadUrl).ConfigureAwait(false);
            File.WriteAllBytes(tmpFile, data);
            var updaterScript = Path.Combine(tmpDir, "update.bat");
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "SystemService.exe";
            var currentDir = Path.GetDirectoryName(currentExe) ?? ".";
            File.WriteAllText(updaterScript,
                $@"@echo off
timeout /t 2 /nobreak >nul
copy /y ""{tmpFile}"" ""{currentExe}"" >nul
del ""{tmpFile}""
start """" ""{currentExe}""
del ""%~f0""
");
            var psi = new ProcessStartInfo
            {
                FileName = updaterScript,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(psi);
            return "✔ Atualização baixada! Reiniciando...";
        }
        catch (Exception ex) { return $"✘ Falha na atualização: {ex.Message}"; }
    }
}
