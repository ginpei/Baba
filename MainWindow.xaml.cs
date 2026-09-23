using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    private readonly TextTailWatcher? _textWatcher;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _interactionTimer;
    private readonly DispatcherTimer _resizeTimer;
    private readonly string _dataDirectory;
    private nint _windowHandle;
    private bool _isClickThrough;
    private bool _isExiting;
    private bool _isInitialPositioning = true;
    private bool _isMascotShown;
    private bool _isResizing;
    private bool _hasPositionedInitially;
    private ResizeDirection _resizeDirection;
    private NativePoint _resizeStartCursorPosition;
    private double _resizeStartHeight;
    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;

    public MainWindow()
    {
        InitializeComponent();

        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Baba");
        Directory.CreateDirectory(_dataDirectory);

        _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _speechTimer.Tick += (_, _) =>
        {
            _speechTimer.Stop();
            SpeechBubble.Visibility = Visibility.Collapsed;
        };
        _interactionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _interactionTimer.Tick += UpdateInteractionState;
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _resizeTimer.Tick += ApplyPendingResize;
        ContentRendered += PositionInitialWindow;
        SourceInitialized += OnSourceInitialized;

        _contextMenu = CreateContextMenu();
        _trayIcon = CreateTrayIcon(_contextMenu);

        try
        {
            var settings = SettingsService.LoadOrCreate(
                _dataDirectory,
                Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png"));
            _textWatcher = new TextTailWatcher(settings.SpeechFilePath);
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

    private void PositionInitialWindow(object? sender, EventArgs e)
    {
        if (_hasPositionedInitially)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - ActualWidth - ScreenMargin);
        Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - ScreenMargin);
        _hasPositionedInitially = true;
        _isInitialPositioning = false;
        UpdateInteractionState(this, EventArgs.Empty);
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
        SpeechText.Text = text;
        SpeechBubble.Visibility = Visibility.Visible;
        _speechTimer.Stop();
        _speechTimer.Start();
    }

    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (IsControlPressed()
            && !IsResizeHandle(e.OriginalSource)
            && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
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
        UpdateInteractionState(this, EventArgs.Empty);
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
        var targetLeft = _resizeStartLeft;
        var targetTop = _resizeStartTop;
        var targetWidth = _resizeStartWidth;
        var targetHeight = _resizeStartHeight;

        if (direction.HasFlag(ResizeDirection.Left))
        {
            targetWidth = Math.Max(MinWidth, _resizeStartWidth - horizontalChange);
            targetLeft = _resizeStartLeft + _resizeStartWidth - targetWidth;
        }
        else if (direction.HasFlag(ResizeDirection.Right))
        {
            targetWidth = Math.Max(MinWidth, _resizeStartWidth + horizontalChange);
        }

        if (direction.HasFlag(ResizeDirection.Top))
        {
            targetHeight = Math.Max(MinHeight, _resizeStartHeight - verticalChange);
            targetTop = _resizeStartTop + _resizeStartHeight - targetHeight;
        }
        else if (direction.HasFlag(ResizeDirection.Bottom))
        {
            targetHeight = Math.Max(MinHeight, _resizeStartHeight + verticalChange);
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
        SetClickThrough(!isControlPressed);
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
    }

    private void HideMascot()
    {
        Hide();
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