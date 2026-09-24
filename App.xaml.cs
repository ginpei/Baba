using System.IO;
using System.Windows;
using Baba.Application;

namespace Baba;

public partial class App : System.Windows.Application
{
    private BabaApplicationSession? _applicationSession;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        try
        {
            _applicationSession = BabaApplicationSession.Create(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Baba"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png"));
            window.Configure(_applicationSession);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or System.Text.Json.JsonException
            or UnauthorizedAccessException)
        {
            window.ShowStartupError(exception);
        }

        MainWindow = window;
        _applicationSession?.Start();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _applicationSession?.Dispose();
        base.OnExit(e);
    }
}
