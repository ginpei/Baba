using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Baba.Services;
using Forms = System.Windows.Forms;

namespace Baba;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const double MaximumWindowHeightRatio = 0.6;
    private const double MaximumWindowSize = 640;
    private const double MaximumWindowWidthRatio = 0.5;
    private const double ScreenMargin = 12;
    private const int VkControl = 0x11;
    private const int WsExTransparent = 0x20;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;

    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly SpeechBubbleWindow _speechWindow;
    private readonly TextTailWatcher? _textWatcher;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _interactionTimer;
    private readonly DispatcherTimer _resizeTimer;
    private readonly string _dataDirectory;
    private BabaSettings? _settings;
    private nint _windowHandle;
    private bool _isClickThrough;
    private bool _isExiting;
    private bool _isInitialPositioning = true;
    private bool _isDragging;
    private bool _isMascotShown;
    private bool _isResizing;
    private bool _isSpeechVisible;
    private bool _hasPositionedInitially;
    private string? _currentSpeechText;
    private ResizeDirection _resizeDirection;
    private NativePoint _resizeStartCursorPosition;
    private double _resizeStartHeight;
    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;
    private NativePoint _dragStartCursorPosition;
    private double _dragStartLeft;
    private double _dragStartTop;

    public MainWindow()
    {
        InitializeComponent();
        _speechWindow = new SpeechBubbleWindow();

        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Baba");
        Directory.CreateDirectory(_dataDirectory);

        _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _speechTimer.Tick += (_, _) =>
        {
            _speechTimer.Stop();
            _isSpeechVisible = false;
            _speechWindow.Hide();
        };
        _interactionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _interactionTimer.Tick += UpdateInteractionState;
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _resizeTimer.Tick += ApplyPendingResize;
        ContentRendered += PositionInitialWindow;
        LocationChanged += (_, _) => PositionSpeechWindow();
        SizeChanged += (_, _) => PositionSpeechWindow();
        SourceInitialized += OnSourceInitialized;

        _contextMenu = CreateContextMenu();
        _trayIcon = CreateTrayIcon(_contextMenu);

        try
        {
            var settings = SettingsService.LoadOrCreate(
                _dataDirectory,
                Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png"));
            _settings = settings;
            ApplySavedWindowSize(settings);
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
        _windowHandle = new WindowInteropHelper(this).Handle;
        SetClickThrough(!IsControlPressed());
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
        _isInitialPositioning = false;
        _speechWindow.Owner = this;
        UpdateInteractionState(this, EventArgs.Empty);
        ShowSpeechWindow();
    }

    private Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowMascot());
        menu.Items.Add("Hide", null, (_, _) => HideMascot());
        menu.Items.Add("Open Data Folder", null, (_, _) => OpenDataDirectory());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private Forms.NotifyIcon CreateTrayIcon(Forms.ContextMenuStrip menu)
    {
        var icon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Text = "Baba",
            Visible = true,
        };
        icon.DoubleClick += (_, _) => ShowMascot();
        return icon;
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
        if (!IsControlPressed()
            || IsResizeHandle(e.OriginalSource)
            || e.LeftButton != MouseButtonState.Pressed
            || !GetCursorPos(out _dragStartCursorPosition))
        {
            return;
        }

        _isDragging = true;
        _dragStartLeft = Left;
        _dragStartTop = Top;
        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void MoveWindow(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || !GetCursorPos(out var cursorPosition))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        Left = _dragStartLeft + (cursorPosition.X - _dragStartCursorPosition.X) / dpi.DpiScaleX;
        Top = _dragStartTop + (cursorPosition.Y - _dragStartCursorPosition.Y) / dpi.DpiScaleY;
        e.Handled = true;
    }

    private void EndWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        MoveWindow(sender, e);
        _isDragging = false;
        Mouse.Capture(null);
        SaveWindowBounds();
        e.Handled = true;
    }

    private void CancelWindowDrag(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void ResizeHandleDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!GetCursorPos(out _resizeStartCursorPosition))
        {
            return;
        }

        _isResizing = true;
        _resizeDirection = ParseResizeDirection(((Thumb)sender).Tag);
        _resizeStartLeft = Left;
        _resizeStartTop = Top;
        _resizeStartWidth = Width;
        _resizeStartHeight = Height;
        _resizeTimer.Start();
    }

    private void ResizeHandleDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizeTimer.Stop();
        ApplyResizeFromCursor();
        _isResizing = false;
        SaveWindowBounds();
        UpdateInteractionState(this, EventArgs.Empty);
    }

    private void ApplySavedWindowSize(BabaSettings settings)
    {
        if (settings.WindowWidth is not { } width
            || settings.WindowHeight is not { } height
            || !double.IsFinite(width)
            || !double.IsFinite(height)
            || width <= 0
            || height <= 0)
        {
            return;
        }

        var maximumSize = GetMaximumWindowSize();
        Width = Math.Clamp(width, MinWidth, maximumSize.Width);
        Height = Math.Clamp(height, MinHeight, maximumSize.Height);
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
            _settings = SettingsService.SaveWindowBounds(_dataDirectory, _settings, Left, Top, Width, Height);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            ShowSpeech($"Could not save window bounds: {exception.Message}");
        }
    }

    private void ApplyPendingResize(object? sender, EventArgs e) => ApplyResizeFromCursor();

    private void ApplyResizeFromCursor()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var horizontalChange = (cursorPosition.X - _resizeStartCursorPosition.X) / dpi.DpiScaleX;
        var verticalChange = (cursorPosition.Y - _resizeStartCursorPosition.Y) / dpi.DpiScaleY;
        ResizeWindow(_resizeDirection, horizontalChange, verticalChange);
    }

    private void ResizeWindow(ResizeDirection direction, double horizontalChange, double verticalChange)
    {
        var maximumSize = GetMaximumWindowSize();
        var targetLeft = _resizeStartLeft;
        var targetTop = _resizeStartTop;
        var targetWidth = _resizeStartWidth;
        var targetHeight = _resizeStartHeight;

        if (direction.HasFlag(ResizeDirection.Left))
        {
            targetWidth = Math.Clamp(_resizeStartWidth - horizontalChange, MinWidth, maximumSize.Width);
            targetLeft = _resizeStartLeft + _resizeStartWidth - targetWidth;
        }
        else if (direction.HasFlag(ResizeDirection.Right))
        {
            targetWidth = Math.Clamp(_resizeStartWidth + horizontalChange, MinWidth, maximumSize.Width);
        }

        if (direction.HasFlag(ResizeDirection.Top))
        {
            targetHeight = Math.Clamp(_resizeStartHeight - verticalChange, MinHeight, maximumSize.Height);
            targetTop = _resizeStartTop + _resizeStartHeight - targetHeight;
        }
        else if (direction.HasFlag(ResizeDirection.Bottom))
        {
            targetHeight = Math.Clamp(_resizeStartHeight + verticalChange, MinHeight, maximumSize.Height);
        }

        BeginInit();
        try
        {
            Width = targetWidth;
            Height = targetHeight;
            Left = targetLeft;
            Top = targetTop;
        }
        finally
        {
            EndInit();
        }
    }

    private (double Width, double Height) GetMaximumWindowSize()
    {
        var workArea = SystemParameters.WorkArea;
        return (
            Math.Max(MinWidth, Math.Min(MaximumWindowSize, workArea.Width * MaximumWindowWidthRatio)),
            Math.Max(MinHeight, Math.Min(MaximumWindowSize, workArea.Height * MaximumWindowHeightRatio)));
    }

    private static ResizeDirection ParseResizeDirection(object? value) =>
        value is string name && Enum.TryParse<ResizeDirection>(name, out var direction)
            ? direction
            : throw new InvalidOperationException("A resize handle requires a valid resize direction.");

    private static bool IsResizeHandle(object source)
    {
        for (var current = source as DependencyObject; current is not null;)
        {
            if (current is Thumb)
            {
                return true;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                FrameworkContentElement contentElement => contentElement.Parent,
                _ => null,
            };
        }

        return false;
    }

    private void ShowContextMenu(object sender, MouseButtonEventArgs e)
    {
        if (!IsControlPressed() || Opacity == 0)
        {
            return;
        }

        _contextMenu.Show(Forms.Cursor.Position);
        e.Handled = true;
    }

    private void UpdateInteractionState(object? sender, EventArgs e)
    {
        if (_isInitialPositioning)
        {
            return;
        }

        var isControlPressed = IsControlPressed();
        SetClickThrough(!isControlPressed && !_isDragging && !_isResizing);
        UpdateResizeControls(isControlPressed && (_isResizing || IsCursorOverMascotArea()));
        if (isControlPressed || !IsCursorOverWindow())
        {
            ShowMascotWithFade();
        }
        else
        {
            HideMascotImmediately();
        }
    }

    private void UpdateResizeControls(bool isVisible)
    {
        var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ResizeFrame.Visibility = visibility;
        ResizeHandles.Visibility = visibility;
    }

    private void ShowMascotWithFade()
    {
        if (_isMascotShown)
        {
            return;
        }

        _isMascotShown = true;
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                FillBehavior = FillBehavior.HoldEnd,
            });
        ShowSpeechWindow();
    }

    private void HideMascotImmediately()
    {
        if (!_isMascotShown)
        {
            return;
        }

        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        _isMascotShown = false;
        _speechWindow.Hide();
    }

    private void SetClickThrough(bool isEnabled)
    {
        if (_windowHandle == nint.Zero || _isClickThrough == isEnabled)
        {
            return;
        }

        var extendedStyle = GetWindowLongPtr(_windowHandle, GwlExStyle);
        var updatedStyle = isEnabled
            ? extendedStyle | (nint)WsExTransparent
            : extendedStyle & ~(nint)WsExTransparent;
        SetWindowLongPtr(_windowHandle, GwlExStyle, updatedStyle);
        SetWindowPos(
            _windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpFrameChanged | SwpNoActivate | SwpNoMove | SwpNoSize | SwpNoZOrder);
        _isClickThrough = isEnabled;
    }

    private bool IsCursorOverWindow()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            return false;
        }

        var topLeft = PointToScreen(new System.Windows.Point(0, 0));
        var bottomRight = PointToScreen(new System.Windows.Point(ActualWidth, ActualHeight));
        return cursorPosition.X >= topLeft.X
            && cursorPosition.X < bottomRight.X
            && cursorPosition.Y >= topLeft.Y
            && cursorPosition.Y < bottomRight.Y;
    }

    private bool IsCursorOverMascotArea()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            return false;
        }

        var topLeft = MascotArea.PointToScreen(new System.Windows.Point(0, 0));
        var bottomRight = MascotArea.PointToScreen(
            new System.Windows.Point(MascotArea.ActualWidth, MascotArea.ActualHeight));
        return cursorPosition.X >= topLeft.X
            && cursorPosition.X < bottomRight.X
            && cursorPosition.Y >= topLeft.Y
            && cursorPosition.Y < bottomRight.Y;
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
        _resizeTimer.Stop();
        _textWatcher?.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
        _speechWindow.Close();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [Flags]
    private enum ResizeDirection
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        TopLeft = Top | Left,
        TopRight = Top | Right,
        BottomLeft = Bottom | Left,
        BottomRight = Bottom | Right,
    }
}