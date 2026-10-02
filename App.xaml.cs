using System.IO;
using System.Windows;
using System.Windows.Threading;
using Baba.Application;
using Baba.Infrastructure;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Baba;

public partial class App : System.Windows.Application
{
    private readonly string _dataDirectory;
    private readonly ApplicationLog _log;
    private BabaApplicationSession? _applicationSession;

    public App()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Baba");
        _log = new ApplicationLog(_dataDirectory);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Forms.Application.ThreadException += OnThreadException;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _log.WriteEvent(
            "Application started",
            $"Version={typeof(App).Assembly.GetName().Version}; OS={Environment.OSVersion}; Runtime={Environment.Version}");

        var window = new MainWindow(_log);
        try
        {
            _applicationSession = BabaApplicationSession.Create(
                _dataDirectory,
                Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "se-progress.wav"));
            window.Configure(_applicationSession);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or System.Text.Json.JsonException
            or UnauthorizedAccessException)
        {
            _log.WriteException("Could not initialize application", exception);
            window.ShowStartupError(exception);
        }

        MainWindow = window;
        _applicationSession?.Start();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.WriteEvent("Application exiting", $"ExitCode={e.ApplicationExitCode}");
        _applicationSession?.Dispose();
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Forms.Application.ThreadException -= OnThreadException;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _log.WriteEvent("Windows session ending", $"Reason={e.ReasonSessionEnding}");
        base.OnSessionEnding(e);
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _log.WriteException("Unhandled WPF dispatcher exception", e.Exception);
        if (!IsRecoverable(e.Exception))
        {
            return;
        }

        e.Handled = true;
        TryShowUnexpectedError(e.Exception);
    }

    private void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        _log.WriteException("Unhandled Windows Forms thread exception", e.Exception);
        TryShowUnexpectedError(e.Exception);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _log.WriteException(
                $"Unhandled AppDomain exception; terminating={e.IsTerminating}",
                exception);
            return;
        }

        _log.WriteEvent(
            "Unhandled AppDomain exception",
            $"Terminating={e.IsTerminating}; Value={e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        _log.WriteException("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        _log.WriteEvent("Windows power mode changed", e.Mode.ToString());
    }

    private void TryShowUnexpectedError(Exception exception)
    {
        try
        {
            if (MainWindow is MainWindow window)
            {
                window.ShowUnexpectedError(exception);
            }
        }
        catch (Exception notificationException)
        {
            _log.WriteException(
                "Could not display unexpected error notification",
                notificationException);
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not (
            AccessViolationException
            or OutOfMemoryException
            or StackOverflowException);
}
