using System.Runtime.InteropServices;

namespace Baba.Presentation;

internal static class NativeInput
{
    private const int VkControl = 0x11;

    public static bool IsControlPressed() => (GetAsyncKeyState(VkControl) & 0x8000) != 0;

    public static bool TryGetCursorPosition(out NativePoint point) => GetCursorPos(out point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}
