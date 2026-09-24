using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Baba.Presentation;

internal sealed class MascotInteractionController
{
    private readonly Func<bool> _isControlPressed;
    private readonly FrameworkElement _mascotArea;
    private readonly UIElement _resizeFrame;
    private readonly UIElement _resizeHandles;
    private readonly Window _window;
    private bool _isClickThrough;
    private bool _isInitiallyPositioning = true;
    private bool _isMascotShown;

    public MascotInteractionController(
        Window window,
        FrameworkElement mascotArea,
        UIElement resizeFrame,
        UIElement resizeHandles,
        Func<bool> isControlPressed)
    {
        _window = window;
        _mascotArea = mascotArea;
        _resizeFrame = resizeFrame;
        _resizeHandles = resizeHandles;
        _isControlPressed = isControlPressed;
    }

    public event EventHandler? MascotHidden;

    public event EventHandler? MascotShown;

    public void Initialize()
    {
        SetClickThrough(!_isControlPressed());
    }

    public void CompleteInitialPositioning()
    {
        _isInitiallyPositioning = false;
    }

    public void Update(bool isResizing)
    {
        if (_isInitiallyPositioning)
        {
            return;
        }

        var isControlPressed = _isControlPressed();
        SetClickThrough(!isControlPressed && !isResizing);
        UpdateResizeControls(isControlPressed && (isResizing || IsCursorOverMascotArea()));
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
        _resizeFrame.Visibility = visibility;
        _resizeHandles.Visibility = visibility;
    }

    private void ShowMascotWithFade()
    {
        if (_isMascotShown)
        {
            return;
        }

        _isMascotShown = true;
        _window.BeginAnimation(
            Window.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                FillBehavior = FillBehavior.HoldEnd,
            });
        MascotShown?.Invoke(this, EventArgs.Empty);
    }

    private void HideMascotImmediately()
    {
        if (!_isMascotShown)
        {
            return;
        }

        _window.BeginAnimation(Window.OpacityProperty, null);
        _window.Opacity = 0;
        _isMascotShown = false;
        MascotHidden?.Invoke(this, EventArgs.Empty);
    }

    private void SetClickThrough(bool isEnabled)
    {
        if (_isClickThrough == isEnabled)
        {
            return;
        }

        NativeWindowStyles.SetClickThrough(_window, isEnabled);
        _isClickThrough = isEnabled;
    }

    private bool IsCursorOverWindow()
    {
        if (!NativeInput.TryGetCursorPosition(out var cursorPosition))
        {
            return false;
        }

        var topLeft = _window.PointToScreen(new System.Windows.Point(0, 0));
        var bottomRight = _window.PointToScreen(
            new System.Windows.Point(_window.ActualWidth, _window.ActualHeight));
        return cursorPosition.X >= topLeft.X
            && cursorPosition.X < bottomRight.X
            && cursorPosition.Y >= topLeft.Y
            && cursorPosition.Y < bottomRight.Y;
    }

    private bool IsCursorOverMascotArea()
    {
        if (!NativeInput.TryGetCursorPosition(out var cursorPosition))
        {
            return false;
        }

        var topLeft = _mascotArea.PointToScreen(new System.Windows.Point(0, 0));
        var bottomRight = _mascotArea.PointToScreen(
            new System.Windows.Point(_mascotArea.ActualWidth, _mascotArea.ActualHeight));
        return cursorPosition.X >= topLeft.X
            && cursorPosition.X < bottomRight.X
            && cursorPosition.Y >= topLeft.Y
            && cursorPosition.Y < bottomRight.Y;
    }

}
