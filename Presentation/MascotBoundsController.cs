using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Baba.Presentation;

internal sealed class MascotBoundsController : IDisposable
{
    private const double MaximumWindowHeightRatio = 0.6;
    private const double MaximumWindowSize = 640;
    private const double MaximumWindowWidthRatio = 0.5;

    private readonly DispatcherTimer _resizeTimer;
    private readonly Window _window;
    private bool _isDragging;
    private ResizeDirection _resizeDirection;
    private NativePoint _resizeStartCursorPosition;
    private double _resizeStartHeight;
    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;
    private NativePoint _dragStartCursorPosition;
    private double _dragStartLeft;
    private double _dragStartTop;

    public MascotBoundsController(Window window)
    {
        _window = window;
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _resizeTimer.Tick += ApplyPendingResize;
    }

    public event EventHandler? BoundsChanged;

    public bool IsResizing { get; private set; }

    public void ApplySavedSize(double? width, double? height)
    {
        if (width is not { } savedWidth
            || height is not { } savedHeight
            || !double.IsFinite(savedWidth)
            || !double.IsFinite(savedHeight)
            || savedWidth <= 0
            || savedHeight <= 0)
        {
            return;
        }

        var maximumSize = GetMaximumSize();
        _window.Width = Math.Clamp(savedWidth, _window.MinWidth, maximumSize.Width);
        _window.Height = Math.Clamp(savedHeight, _window.MinHeight, maximumSize.Height);
    }

    public bool TryBeginDrag(object source, MouseButtonEventArgs e)
    {
        if (IsResizeHandle(e.OriginalSource)
            || e.LeftButton != MouseButtonState.Pressed
            || !NativeInput.TryGetCursorPosition(out _dragStartCursorPosition))
        {
            return false;
        }

        _isDragging = true;
        _dragStartLeft = _window.Left;
        _dragStartTop = _window.Top;
        Mouse.Capture((IInputElement)source);
        return true;
    }

    public bool MoveDrag()
    {
        if (!_isDragging || !NativeInput.TryGetCursorPosition(out var cursorPosition))
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(_window);
        _window.Left = _dragStartLeft + (cursorPosition.X - _dragStartCursorPosition.X) / dpi.DpiScaleX;
        _window.Top = _dragStartTop + (cursorPosition.Y - _dragStartCursorPosition.Y) / dpi.DpiScaleY;
        return true;
    }

    public bool EndDrag()
    {
        if (!_isDragging)
        {
            return false;
        }

        MoveDrag();
        _isDragging = false;
        Mouse.Capture(null);
        BoundsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void CancelDrag() => _isDragging = false;

    public void BeginResize(Thumb resizeHandle)
    {
        if (!NativeInput.TryGetCursorPosition(out _resizeStartCursorPosition))
        {
            return;
        }

        IsResizing = true;
        _resizeDirection = ParseResizeDirection(resizeHandle.Tag);
        _resizeStartLeft = _window.Left;
        _resizeStartTop = _window.Top;
        _resizeStartWidth = _window.Width;
        _resizeStartHeight = _window.Height;
        _resizeTimer.Start();
    }

    public void CompleteResize()
    {
        if (!IsResizing)
        {
            return;
        }

        _resizeTimer.Stop();
        ApplyResizeFromCursor();
        IsResizing = false;
        BoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _resizeTimer.Stop();
    }

    private void ApplyPendingResize(object? sender, EventArgs e) => ApplyResizeFromCursor();

    private void ApplyResizeFromCursor()
    {
        if (!NativeInput.TryGetCursorPosition(out var cursorPosition))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(_window);
        var horizontalChange = (cursorPosition.X - _resizeStartCursorPosition.X) / dpi.DpiScaleX;
        var verticalChange = (cursorPosition.Y - _resizeStartCursorPosition.Y) / dpi.DpiScaleY;
        ResizeWindow(_resizeDirection, horizontalChange, verticalChange);
    }

    private void ResizeWindow(ResizeDirection direction, double horizontalChange, double verticalChange)
    {
        var maximumSize = GetMaximumSize();
        var targetLeft = _resizeStartLeft;
        var targetTop = _resizeStartTop;
        var targetWidth = _resizeStartWidth;
        var targetHeight = _resizeStartHeight;

        if (direction.HasFlag(ResizeDirection.Left))
        {
            targetWidth = Math.Clamp(_resizeStartWidth - horizontalChange, _window.MinWidth, maximumSize.Width);
            targetLeft = _resizeStartLeft + _resizeStartWidth - targetWidth;
        }
        else if (direction.HasFlag(ResizeDirection.Right))
        {
            targetWidth = Math.Clamp(_resizeStartWidth + horizontalChange, _window.MinWidth, maximumSize.Width);
        }

        if (direction.HasFlag(ResizeDirection.Top))
        {
            targetHeight = Math.Clamp(_resizeStartHeight - verticalChange, _window.MinHeight, maximumSize.Height);
            targetTop = _resizeStartTop + _resizeStartHeight - targetHeight;
        }
        else if (direction.HasFlag(ResizeDirection.Bottom))
        {
            targetHeight = Math.Clamp(_resizeStartHeight + verticalChange, _window.MinHeight, maximumSize.Height);
        }

        _window.BeginInit();
        try
        {
            _window.Width = targetWidth;
            _window.Height = targetHeight;
            _window.Left = targetLeft;
            _window.Top = targetTop;
        }
        finally
        {
            _window.EndInit();
        }
    }

    private (double Width, double Height) GetMaximumSize()
    {
        var workArea = SystemParameters.WorkArea;
        return (
            Math.Max(_window.MinWidth, Math.Min(MaximumWindowSize, workArea.Width * MaximumWindowWidthRatio)),
            Math.Max(_window.MinHeight, Math.Min(MaximumWindowSize, workArea.Height * MaximumWindowHeightRatio)));
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
