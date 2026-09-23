using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Baba.Services;
using Forms = System.Windows.Forms;

namespace Baba;

public partial class MainWindow : Window
{
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly TextTailWatcher _textWatcher;
    private readonly DispatcherTimer _speechTimer;
    private readonly string _dataDirectory;
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

        _trayIcon = CreateTrayIcon();
        _textWatcher = new TextTailWatcher(Path.Combine(_dataDirectory, "speech.txt"));
        _textWatcher.LastLineChanged += OnLastLineChanged;
        _textWatcher.ReadFailed += OnTextReadFailed;
        _textWatcher.Start();

        LoadMascotImage();
        ShowSpeech($"Monitoring speech file:{Environment.NewLine}{_dataDirectory}\\speech.txt");
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowMascot());
        menu.Items.Add("Hide", null, (_, _) => Hide());
        menu.Items.Add("Open Data Folder", null, (_, _) => OpenDataDirectory());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

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

    private void LoadMascotImage()
    {
        var customImagePath = Path.Combine(_dataDirectory, "mascot.png");
        var defaultImagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png");
        var imagePath = File.Exists(customImagePath) ? customImagePath : defaultImagePath;
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
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ShowMascot()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _textWatcher.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }
}