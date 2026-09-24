# Baba

Baba is a WPF desktop mascot. Use the notification-area icon to show, hide, or exit the application.

## Build and Run

```powershell
dotnet build
dotnet run
```

Run the unit tests with:

```powershell
dotnet test Baba.Tests\Baba.Tests.csproj
```

On first launch, Baba creates the following directory and an empty `moments.txt` file:

```text
%LocalAppData%\Baba
```

Appending a non-whitespace line to `moments.txt` displays that line in the speech bubble.

For speech files on UNC paths, including `\\wsl.localhost\...`, Baba polls once per second as a fallback because file-change notifications from network-backed filesystems can be unreliable.

The project-provided `Assets\mascot.png` is the default image. To use a different transparent PNG, place it as `mascot.png` in the following location and restart the application. The custom image takes precedence over the default.

```text
%LocalAppData%\Baba\mascot.png
```

If the default image is missing or cannot be loaded, Baba displays a placeholder mascot.

The speech bubble has a fixed width and follows the mascot independently, so resizing the mascot does not change the bubble's dimensions.

## Configuration

On first launch, Baba creates `%LocalAppData%\Baba\baba.json`, `%LocalAppData%\Baba\moments.txt`, and `%LocalAppData%\Baba\mascot.png`. The text file starts with brief usage instructions, and the image is copied from the bundled default.

`baba.json` contains absolute paths for the speech and image files. Edit either path to use files stored elsewhere. After a move or resize, it also stores the current window bounds:

```json
{
  "MascotImagePath": "C:\\Users\\<user>\\AppData\\Local\\Baba\\mascot.png",
  "SpeechSources": [
    {
      "SpeechFilePath": "C:\\Users\\<user>\\AppData\\Local\\Baba\\moments.txt"
    },
    {
      "SpeechFilePath": "C:\\logs\\application.log",
      "SpeechLinePattern": "^- ....-..-.. ..:..:.. (.+)$"
    }
  ],
  "WindowWidth": 320,
  "WindowHeight": 390,
  "WindowLeft": 1588,
  "WindowTop": 666
}
```

When a configured speech or image file is missing, Baba creates the speech file or copies the bundled default image to the configured location.

## Speech Line Pattern

Log lines can include metadata such as timestamp. Each log entry must occupy one line.

For example, if your log line is formatted as `- YYYY-MM-DD HH:mm:ss <message>` like the following:

```
- 2026-09-23 17:30:01 Summarizing nicely.
- 2026-09-23 17:37:24 Reply file is ready!
```

Each entry in `SpeechSources` monitors one file. Set its optional `SpeechLinePattern` to extract speech from matching lines:

```json
{
  "SpeechSources": [
    {
      "SpeechFilePath": "C:\\logs\\application.log",
      "SpeechLinePattern": "^- ....-..-.. ..:..:.. (.+)$"
    }
  ]
}
```

The expression must contain a first capture group for the speech text.

When it is omitted, Baba displays the entire non-empty line.

## Interaction

Mouse input passes through Baba to the window behind it. Baba hides while the pointer is over it and reappears when the pointer leaves. Hold Ctrl while hovering over the mascot to reveal its resize frame. Click the mascot to display the latest message again or restart its display timer, drag a frame edge or corner to resize the window, drag within the mascot to move it, or right-click it to open the notification-area menu.
