using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Baba.Presentation;

internal static class WindowPlacement
{
    public static bool IsVisibleOnAnyScreen(Window window)
    {
        var windowHandle = new WindowInteropHelper(window).Handle;
        if (windowHandle == nint.Zero || !GetWindowRect(windowHandle, out var windowRect))
        {
            return false;
        }

        var windowBounds = Rectangle.FromLTRB(
            windowRect.Left,
            windowRect.Top,
            windowRect.Right,
            windowRect.Bottom);
        var workingAreas = Screen.AllScreens.Select((v) => v.WorkingArea);
        return IsVisibleInAnyWorkingArea(windowBounds, workingAreas);
    }

    internal static bool IsVisibleInAnyWorkingArea(
        Rectangle windowBounds,
        IEnumerable<Rectangle> workingAreas)
    {
        foreach (var workingArea in workingAreas)
        {
            if (windowBounds.IntersectsWith(workingArea))
            {
                return true;
            }
        }

        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rectangle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }
}
