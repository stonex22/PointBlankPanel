using System;
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

public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
    [JsonPropertyName("body")] public string Body { get; set; } = "";
    [JsonPropertyName("assets")] public GitHubAsset[] Assets { get; set; } = [];
}

public class GitHubAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
}

public static class AutoUpdater
{
    public static string UpdateCheckUrl { get; set; } = "https://api.github.com/repos/stonex22/PointBlankPanel/releases/latest";
    public static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("User-Agent", "PointBlankPanel");
            var json = await http.GetStringAsync(UpdateCheckUrl).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release == null || release.Assets.Length == 0) return null;
            var tag = release.TagName.TrimStart('v');
            if (tag == CurrentVersion) return null;
            var asset = release.Assets[0];
            return new UpdateInfo
            {
                Version = tag,
                DownloadUrl = asset.BrowserDownloadUrl,
                Changelog = release.Body,
            };
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
            http.DefaultRequestHeaders.Add("User-Agent", "PointBlankPanel");
            var data = await http.GetByteArrayAsync(info.DownloadUrl).ConfigureAwait(false);
            File.WriteAllBytes(tmpFile, data);
            var updaterScript = Path.Combine(tmpDir, "update.bat");
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "SystemService.exe";
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
