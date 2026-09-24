using System.Windows;
using System.Windows.Input;

namespace Baba;

public partial class SpeechBubbleWindow : Window
{
    public SpeechBubbleWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? DismissRequested;

    public void SetSpeech(string text)
    {
        SpeechText.Text = text;
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
}
