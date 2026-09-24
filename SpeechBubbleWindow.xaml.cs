using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Baba;

public partial class SpeechBubbleWindow : Window
{
    private const double DismissedScale = 0.96;
    private const double HiddenOffset = 6;
    private const double HiddenScale = 0.92;
    private static readonly Duration HideDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly Duration ShowDuration = new(TimeSpan.FromMilliseconds(160));
    private int _animationGeneration;

    public SpeechBubbleWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? DismissRequested;

    public void SetSpeech(string text)
    {
        SpeechText.Text = text;
    }

    public void AnimateIn()
    {
        var generation = ++_animationGeneration;
        var currentOpacity = Bubble.Opacity;
        var currentOffset = BubbleTranslation.Y;
        var currentScale = BubbleScale.ScaleX;
        StopAnimations();
        Bubble.Opacity = 1;
        BubbleScale.ScaleX = 1;
        BubbleScale.ScaleY = 1;
        BubbleTranslation.Y = 0;

        var opacityAnimation = CreateAnimation(
            currentOpacity,
            1,
            ShowDuration,
            new CubicEase { EasingMode = EasingMode.EaseOut });
        var scaleAnimation = CreateAnimation(
            currentScale,
            1,
            ShowDuration,
            new BackEase
            {
                Amplitude = 0.15,
                EasingMode = EasingMode.EaseOut,
            });
        var translationAnimation = CreateAnimation(
            currentOffset,
            0,
            ShowDuration,
            new CubicEase { EasingMode = EasingMode.EaseOut });
        opacityAnimation.Completed += (_, _) =>
        {
            if (generation != _animationGeneration)
            {
                return;
            }

            StopAnimations();
        };

        Bubble.BeginAnimation(OpacityProperty, opacityAnimation);
        BubbleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
        BubbleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
        BubbleTranslation.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            translationAnimation);
    }

    public void AnimateOut(bool hideAfterAnimation)
    {
        if (!IsVisible)
        {
            return;
        }

        var generation = ++_animationGeneration;
        var currentOpacity = Bubble.Opacity;
        var currentOffset = BubbleTranslation.Y;
        var currentScale = BubbleScale.ScaleX;
        StopAnimations();
        Bubble.Opacity = 0;
        BubbleScale.ScaleX = DismissedScale;
        BubbleScale.ScaleY = DismissedScale;
        BubbleTranslation.Y = HiddenOffset / 2;

        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var opacityAnimation = CreateAnimation(currentOpacity, 0, HideDuration, easing);
        var scaleAnimation = CreateAnimation(currentScale, DismissedScale, HideDuration, easing);
        var translationAnimation = CreateAnimation(
            currentOffset,
            HiddenOffset / 2,
            HideDuration,
            easing);
        opacityAnimation.Completed += (_, _) =>
        {
            if (generation != _animationGeneration)
            {
                return;
            }

            StopAnimations();
            if (hideAfterAnimation)
            {
                Hide();
            }
        };

        Bubble.BeginAnimation(OpacityProperty, opacityAnimation);
        BubbleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
        BubbleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
        BubbleTranslation.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            translationAnimation);
    }

    public void HideImmediately()
    {
        _animationGeneration++;
        StopAnimations();
        Hide();
    }

    public void PrepareToShow()
    {
        if (IsVisible)
        {
            return;
        }

        _animationGeneration++;
        StopAnimations();
        Bubble.Opacity = 0;
        BubbleScale.ScaleX = HiddenScale;
        BubbleScale.ScaleY = HiddenScale;
        BubbleTranslation.Y = HiddenOffset;
        Show();
    }

    private static DoubleAnimation CreateAnimation(
        double from,
        double to,
        Duration duration,
        IEasingFunction easingFunction)
    {
        var animation = new DoubleAnimation(from, to, duration)
        {
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.Stop,
        };
        return animation;
    }

    private void DismissSpeech(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        DismissRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void StopAnimations()
    {
        Bubble.BeginAnimation(OpacityProperty, null);
        BubbleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        BubbleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        BubbleTranslation.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
    }
}
