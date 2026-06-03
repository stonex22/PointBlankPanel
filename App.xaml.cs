using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace PointBlankPanel;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        System.Threading.Thread.Sleep(4500);

        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (dict != null && dict.TryGetValue("Iniciar Minimizado", out var min) && min)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (MainWindow != null) MainWindow.WindowState = WindowState.Minimized;
                    });
                }
            }
        }
        catch { }
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        Services.MemoryService.RestoreTimerResolution();
    }

    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Erro:\n{e.Exception.Message}", "System Service Host", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
