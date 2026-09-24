using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Baba.Infrastructure;
using Baba.Services;

namespace Baba;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 12;
    private const int VkControl = 0x11;

    private readonly MascotBoundsController _boundsController;
    private readonly MascotInteractionController _interactionController;
    private readonly SettingsRepository _settingsRepository;
    private readonly TrayIconService _trayIcon;
    private readonly SpeechBubbleWindow _speechWindow;
    private readonly TextTailWatcher? _textWatcher;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _interactionTimer;
    private readonly string _dataDirectory;
    private BabaSettings? _settings;
    private bool _isExiting;
    private bool _isSpeechVisible;
    private bool _hasPositionedInitially;
    private string? _currentSpeechText;

    public MainWindow()
    {
        InitializeComponent();
        _speechWindow = new SpeechBubbleWindow();
        _boundsController = new MascotBoundsController(this);
        _boundsController.BoundsChanged += (_, _) => SaveWindowBounds();
        _interactionController = new MascotInteractionController(
            this,
            MascotArea,
            ResizeFrame,
            ResizeHandles,
            IsControlPressed);
        _interactionController.MascotHidden += (_, _) => _speechWindow.Hide();
        _interactionController.MascotShown += (_, _) => ShowSpeechWindow();

        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Baba");
        _settingsRepository = new SettingsRepository(_dataDirectory);

        _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _speechTimer.Tick += (_, _) =>
        {
            _speechTimer.Stop();
            _isSpeechVisible = false;
            _speechWindow.Hide();
        };
        _interactionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _interactionTimer.Tick += UpdateInteractionState;
        ContentRendered += PositionInitialWindow;
        LocationChanged += (_, _) => PositionSpeechWindow();
        SizeChanged += (_, _) => PositionSpeechWindow();
        SourceInitialized += OnSourceInitialized;

        _trayIcon = new TrayIconService(
            ShowMascot,
            HideMascot,
            OpenDataDirectory,
            ExitApplication);

        try
        {
            var settings = BabaDataDirectoryInitializer.Initialize(
                _settingsRepository,
                Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png"));
            _settings = settings;
            _boundsController.ApplySavedSize(settings.WindowWidth, settings.WindowHeight);
            _textWatcher = new TextTailWatcher(
                settings.SpeechFilePath,
                CreateSpeechLinePattern(settings.SpeechLinePattern));
            _textWatcher.LastLineChanged += OnLastLineChanged;
            _textWatcher.ReadFailed += OnTextReadFailed;
            _textWatcher.Start();

            LoadMascotImage(settings.MascotImagePath);
            ShowSpeech($"Monitoring speech file:{Environment.NewLine}{settings.SpeechFilePath}");
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or JsonException
            or UnauthorizedAccessException)
        {
            ShowSpeech($"Could not load Baba settings: {exception.Message}");
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _interactionController.Initialize();
        _interactionTimer.Start();
    }

    private static Regex? CreateSpeechLinePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var regex = new Regex(
            pattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        if (regex.GetGroupNumbers().Length < 2)
        {
            throw new ArgumentException(
                "SpeechLinePattern must contain a capture group for the speech text.",
                nameof(pattern));
        }

        return regex;
    }

    private void PositionInitialWindow(object? sender, EventArgs e)
    {
        if (_hasPositionedInitially)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        if (!ApplySavedWindowPosition())
        {
            Left = Math.Max(workArea.Left, workArea.Right - ActualWidth - ScreenMargin);
            Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - ScreenMargin);
        }
        _hasPositionedInitially = true;
        _speechWindow.Owner = this;
        _interactionController.CompleteInitialPositioning();
        UpdateInteractionState(this, EventArgs.Empty);
        ShowSpeechWindow();
    }

    private void OpenDataDirectory()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _dataDirectory,
                UseShellExecute = true,
            });
        }
        catch (Win32Exception exception)
        {
            ShowSpeech($"Could not open data folder: {exception.Message}");
        }
    }

    private void LoadMascotImage(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new System.Uri(imagePath, System.UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            MascotImage.Source = image;
            MascotImage.Visibility = Visibility.Visible;
            PlaceholderMascot.Visibility = Visibility.Collapsed;
        }
        catch (NotSupportedException exception)
        {
            ShowSpeech($"Could not load PNG: {exception.Message}");
        }
        catch (IOException exception)
        {
            ShowSpeech($"Could not load PNG: {exception.Message}");
        }
    }

    private void OnLastLineChanged(object? sender, string line)
    {
        _ = Dispatcher.InvokeAsync(() => ShowSpeech(line));
    }

    private void OnTextReadFailed(object? sender, Exception exception)
    {
        _ = Dispatcher.InvokeAsync(() => ShowSpeech($"Could not read speech file: {exception.Message}"));
    }

    private void ShowSpeech(string text)
    {
        _currentSpeechText = text;
        _isSpeechVisible = true;
        ShowSpeechWindow();
        _speechTimer.Stop();
        _speechTimer.Start();
    }

    private void ShowSpeechWindow()
    {
        if (!_hasPositionedInitially || !_isSpeechVisible || _currentSpeechText is null)
        {
            return;
        }

        _speechWindow.SetSpeech(_currentSpeechText);
        if (!_speechWindow.IsVisible)
        {
            _speechWindow.Opacity = 0;
            _speechWindow.Show();
        }

        _speechWindow.UpdateLayout();
        PositionSpeechWindow();
        _speechWindow.Opacity = 1;
    }

    private void PositionSpeechWindow()
    {
        if (!_speechWindow.IsVisible)
        {
            return;
        }

        _speechWindow.UpdateLayout();

        var workArea = SystemParameters.WorkArea;
        var width = _speechWindow.ActualWidth;
        var height = _speechWindow.ActualHeight;
        var left = Left + (ActualWidth - width) / 2;
        var top = Top - height - 8;
        if (top < workArea.Top)
        {
            top = Top + ActualHeight + 8;
        }

        _speechWindow.Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        _speechWindow.Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
    }

    private void BeginWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (!IsControlPressed() || !_boundsController.TryBeginDrag(sender, e))
        {
            return;
        }

        e.Handled = true;
    }

    private void MoveWindow(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_boundsController.MoveDrag())
        {
            return;
        }

        e.Handled = true;
    }

    private void EndWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_boundsController.EndDrag())
        {
            return;
        }

        e.Handled = true;
    }

    private void CancelWindowDrag(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _boundsController.CancelDrag();
    }

    private void ResizeHandleDragStarted(object sender, DragStartedEventArgs e)
    {
        _boundsController.BeginResize((Thumb)sender);
    }

    private void ResizeHandleDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _boundsController.CompleteResize();
        UpdateInteractionState(this, EventArgs.Empty);
    }

    private bool ApplySavedWindowPosition()
    {
        if (_settings?.WindowLeft is not { } left
            || _settings.WindowTop is not { } top
            || !double.IsFinite(left)
            || !double.IsFinite(top))
        {
            return false;
        }

        Left = left;
        Top = top;
        return true;
    }

    private void SaveWindowBounds()
    {
        if (_settings is null)
        {
            return;
        }

        try
        {
            _settings = _settings.WithWindowBounds(Left, Top, Width, Height);
            _settingsRepository.Save(_settings);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            ShowSpeech($"Could not save window bounds: {exception.Message}");
        }
    }


    private void ShowContextMenu(object sender, MouseButtonEventArgs e)
    {
        if (!IsControlPressed() || Opacity == 0)
        {
            return;
        }

        _trayIcon.ShowContextMenuAtCursor();
        e.Handled = true;
    }

    private void UpdateInteractionState(object? sender, EventArgs e)
    {
        _interactionController.Update(_boundsController.IsResizing);
    }

    private static bool IsControlPressed() => (GetAsyncKeyState(VkControl) & 0x8000) != 0;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        HideMascot();
    }

    private void ShowMascot()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        ShowSpeechWindow();
    }

    private void HideMascot()
    {
        Hide();
        _speechWindow.Hide();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _interactionTimer.Stop();
        _boundsController.Dispose();
        _textWatcher?.Dispose();
        _trayIcon.Dispose();
        _speechWindow.Close();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

}