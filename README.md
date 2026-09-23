# Baba

Baba is a WPF desktop mascot. Use the notification-area icon to show, hide, or exit the application.

## Run

```powershell
dotnet run
```

On first launch, Baba creates the following directory and an empty `speech.txt` file:

```text
%LocalAppData%\Baba
```

Appending a non-whitespace line to `speech.txt` displays that line in the speech bubble.

The project-provided `Assets\mascot.png` is the default image. To use a different transparent PNG, place it as `mascot.png` in the following location and restart the application. The custom image takes precedence over the default.

```text
%LocalAppData%\Baba\mascot.png
```

If the default image is missing or cannot be loaded, Baba displays a placeholder mascot.

## Interaction

Mouse input passes through Baba to the window behind it. Baba hides while the pointer is over it and reappears when the pointer leaves. Hold Ctrl to keep Baba visible, drag it with the left mouse button, or right-click it to open the notification-area menu.
