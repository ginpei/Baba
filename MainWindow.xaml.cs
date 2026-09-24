using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Baba.Application;
using Baba.Configuration;
using Baba.Presentation;

namespace Baba;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 12;
    private readonly MascotBoundsController _boundsController;
    private readonly MascotInteractionController _interactionController;
    private readonly TrayIconService _trayIcon;
    private readonly SpeechBubbleWindow _speechWindow;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _interactionTimer;
    private BabaApplicationSession? _applicationSession;
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
            _speechWindow,
            MascotArea,
            ResizeFrame,
            ResizeHandles,
            NativeInput.IsControlPressed);
        _interactionController.SpeechBubbleHidden += (_, _) =>
        {
            if (_isSpeechVisible)
            {
                _speechWindow.HideForPointer();
            }
        };
        _interactionController.SpeechBubbleShown += (_, _) => ShowSpeechWindow();
        _speechWindow.DismissRequested += (_, _) => DismissSpeech();

        _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _speechTimer.Tick += (_, _) => DismissSpeech();
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

    }

    internal void Configure(BabaApplicationSession applicationSession)
    {
        _applicationSession = applicationSession;
        _settings = applicationSession.Settings;
        _boundsController.ApplySavedSize(_settings.WindowWidth, _settings.WindowHeight);
        applicationSession.SpeechUpdated += OnLastLineChanged;
        applicationSession.SpeechReadFailed += OnTextReadFailed;

        LoadMascotImage(_settings.MascotImagePath);
    }

    internal void ShowStartupError(Exception exception) =>
        ShowSpeech($"Could not load Baba settings: {exception.Message}");

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _interactionController.Initialize();
        _interactionTimer.Start();
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
        if (_applicationSession is null)
        {
            ShowSpeech("Could not open data folder: Baba has not been configured.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _applicationSession.DataDirectory,
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
        ShowCurrentSpeech();
    }

    private void ShowCurrentSpeech()
    {
        if (_currentSpeechText is null)
        {
            return;
        }

        _isSpeechVisible = true;
        ShowSpeechWindow();
        _speechTimer.Stop();
        _speechTimer.Start();
    }

    private void ShowSpeechWindow()
    {
        if (!_hasPositionedInitially
            || !_interactionController.IsSpeechBubbleShown
            || !_isSpeechVisible
            || _currentSpeechText is null)
        {
            return;
        }

        _speechWindow.SetSpeech(_currentSpeechText);
        _speechWindow.PrepareToShow();
        _speechWindow.UpdateLayout();
        PositionSpeechWindow();
        _speechWindow.AnimateIn();
    }

    private void DismissSpeech()
    {
        _speechTimer.Stop();
        _isSpeechVisible = false;
        _speechWindow.DismissAnimated();
    }

    private void PositionSpeechWindow()
    {
        if (!_speechWindow.IsVisible)
        {
            return;
        }

        _speechWindow.UpdateLayout();

        var workArea = GetMascotWorkArea();
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

    private Rect GetMascotWorkArea()
    {
        var mascotCenter = PointToScreen(
            new System.Windows.Point(ActualWidth / 2, ActualHeight / 2));
        var screenPoint = new System.Drawing.Point(
            (int)Math.Round(mascotCenter.X),
            (int)Math.Round(mascotCenter.Y));
        var workingArea = System.Windows.Forms.Screen.FromPoint(screenPoint).WorkingArea;
        var relativeTopLeft = PointFromScreen(
            new System.Windows.Point(workingArea.Left, workingArea.Top));
        var relativeBottomRight = PointFromScreen(
            new System.Windows.Point(workingArea.Right, workingArea.Bottom));
        var workArea = new Rect(
            Left + relativeTopLeft.X,
            Top + relativeTopLeft.Y,
            relativeBottomRight.X - relativeTopLeft.X,
            relativeBottomRight.Y - relativeTopLeft.Y);
        return workArea;
    }

    private void BeginWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (!NativeInput.IsControlPressed() || !_boundsController.TryBeginDrag(sender, e))
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
        if (!_boundsController.EndDrag(out var hasMoved))
        {
            return;
        }

        if (!hasMoved)
        {
            ShowCurrentSpeech();
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
            _applicationSession?.SaveSettings(_settings);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            ShowSpeech($"Could not save window bounds: {exception.Message}");
        }
    }


    private void ShowContextMenu(object sender, MouseButtonEventArgs e)
    {
        if (!NativeInput.IsControlPressed() || Opacity == 0)
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
        _speechWindow.HideImmediately();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _interactionTimer.Stop();
        _boundsController.Dispose();
        _trayIcon.Dispose();
        _speechWindow.Close();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

}