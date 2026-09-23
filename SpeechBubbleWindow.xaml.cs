using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Baba;

public partial class SpeechBubbleWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;

    public SpeechBubbleWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public void SetSpeech(string text)
    {
        SpeechText.Text = text;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLongPtr(windowHandle, GwlExStyle);
        SetWindowLongPtr(windowHandle, GwlExStyle, extendedStyle | (nint)WsExTransparent);
        SetWindowPos(
            windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpFrameChanged | SwpNoActivate | SwpNoMove | SwpNoSize | SwpNoZOrder);
    }

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
}
