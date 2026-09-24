using System.Windows;
using Baba.Infrastructure;

namespace Baba;

public partial class SpeechBubbleWindow : Window
{
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
        NativeWindowStyles.SetClickThrough(this, true);
    }
}
