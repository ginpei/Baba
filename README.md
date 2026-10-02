# Baba

Baba is a WPF desktop mascot.

## Build and Run

```powershell
dotnet build
dotnet run
```

To try a release build:

```powershell
dotnet build --configuration Release
bin\Release\net10.0-windows\Baba.exe
```

Run the unit tests with:

```powershell
dotnet test Baba.Tests\Baba.Tests.csproj
```

On first launch, Baba creates the following directory and a `moments.txt` file containing brief usage instructions:

```text
%LocalAppData%\Baba
```

Append a line in the fixed format `- YYYY-MM-DD HH:mm:ss <message>` to `moments.txt`. Baba displays the message after the timestamp in the speech bubble.

For speech files on UNC paths, including `\\wsl.localhost\...`, Baba polls once per second as a fallback because file-change notifications from network-backed filesystems can be unreliable.

The project-provided `Assets\mascot.png` is the default image. To use a different transparent PNG, place it as `mascot.png` in the following location and restart the application. The custom image takes precedence over the default.

```text
%LocalAppData%\Baba\mascot.png
```

If the default image is missing or cannot be loaded, Baba displays a placeholder mascot.

The project-provided `Assets\se-progress.wav` is the default new-message sound. To use a different WAV file, update `MessageSoundPath` in `baba.json` and restart the application. Set `MessageSoundPath` to an empty string (`""`) to disable the sound. Redisplaying the current message does not play the sound again.

The speech bubble has a fixed width and follows the mascot independently, so resizing the mascot does not change the bubble's dimensions.

## Configuration

On first launch, Baba creates `%LocalAppData%\Baba\baba.json`, `%LocalAppData%\Baba\moments.txt`, `%LocalAppData%\Baba\mascot.png`, and `%LocalAppData%\Baba\se-progress.wav`. The text file starts with brief usage instructions, and the image and sound are copied from the bundled defaults.

`baba.json` contains absolute paths for the speech, image, and sound files. Edit these paths to use files stored elsewhere. After a move or resize, it also stores the current window bounds:

```json
{
  "MascotImagePath": "C:\\Users\\<user>\\AppData\\Local\\Baba\\mascot.png",
  "MessageSoundPath": "C:\\Users\\<user>\\AppData\\Local\\Baba\\se-progress.wav",
  "SpeechSources": [
    {
      "SpeechFilePath": "C:\\Users\\<user>\\AppData\\Local\\Baba\\moments.txt"
    },
    {
      "SpeechFilePath": "C:\\logs\\application.log"
    }
  ],
  "WindowWidth": 320,
  "WindowHeight": 390,
  "WindowLeft": 1588,
  "WindowTop": 666
}
```

When a configured speech, image, or sound file is missing, Baba creates the speech file or copies the corresponding bundled default asset to the configured location.

## Speech Line Format

Each speech entry must occupy one line and use this fixed format:

```text
- YYYY-MM-DD HH:mm:ss <message>
```

The application uses the regular expression `/^- ....-..-.. ..:..:.. (.+)$/` and displays the trimmed content of its first capture group.

For example:

```
- 2026-09-23 17:30:01 Summarizing nicely.
- 2026-09-23 17:37:24 Reply file is ready!
```

Each entry in `SpeechSources` monitors one file:

```json
{
  "SpeechSources": [
    {
      "SpeechFilePath": "C:\\logs\\application.log"
    }
  ]
}
```

## Interaction

Mouse input passes through the mascot to the window behind it. Hold Ctrl while hovering over the mascot to interact.

## Credit

- Default sound `se-progress.wav` - converted from [OtoLogic](https://otologic.jp/)'s ["アニメモーション34-1(短　中)"](https://otologic.jp/free/se/anime-motion03.html) MP3, and used under the [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) license.
