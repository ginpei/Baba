using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Baba.Services;
using Forms = System.Windows.Forms;

namespace Baba;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
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
    private readonly string _dataDirectory;
    private nint _windowHandle;
    private bool _isClickThrough;
    private bool _isExiting;

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
        if (IsControlPressed() && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
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
        var isControlPressed = IsControlPressed();
        SetClickThrough(!isControlPressed);
        Opacity = isControlPressed || !IsCursorOverWindow() ? 1 : 0;
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
}