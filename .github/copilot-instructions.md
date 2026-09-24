# Baba Copilot Instructions

## Build and run

This is a .NET 10 WPF Windows application (`net10.0-windows`), so build and run it from Windows:

```powershell
dotnet build
dotnet run
```

There is currently no test project, test runner, or lint configuration in the repository; consequently, there is no full-suite or single-test command. Validate UI- and Windows-interop changes by running the application.

## Architecture

- `App` is the composition root. On startup it creates a `BabaApplicationSession` for `%LocalAppData%\Baba`, configures `MainWindow`, and starts the session. Shutdown disposes the session.
- `Application\BabaApplicationSession` coordinates settings initialization and speech-file monitoring. `BabaDataDirectoryInitializer` creates or repairs the configured speech file and mascot image, while `SettingsRepository` owns `baba.json` serialization.
- `Infrastructure\TextTailWatcher` observes the configured speech file, debounces file events for 300 ms, reads its final non-empty eligible line, and raises speech events. UNC paths also poll once per second because their filesystem events may be unreliable.
- `MainWindow` owns WPF composition: mascot presentation, speech timing/placement, window-bound persistence, and tray-menu actions. Speech notifications can arrive on a worker thread, so it dispatches them to the WPF dispatcher before updating UI.
- `Presentation` encapsulates platform-specific behavior. `MascotInteractionController` controls opacity, click-through, and resize affordances; `MascotBoundsController` handles DPI-aware dragging and resizing; `NativeInput` and `NativeWindowStyles` wrap `user32.dll`; `TrayIconService` uses Windows Forms for the notification-area icon.
- `SpeechBubbleWindow` is a separate, owned, click-through topmost WPF window. Keep its fixed width independent from mascot dimensions and reposition it whenever mascot location or size changes.

## Repository conventions and runtime contracts

- Keep `BabaSettings` effectively immutable: preserve all fields when creating a replacement via `WithWindowBounds`, then persist it through `BabaApplicationSession.SaveSettings`. Settings accept relative speech/image paths, which are resolved relative to the data directory during initialization.
- `SpeechLinePattern` is optional. When supplied, it must have a first capture group; the watcher displays the trimmed content of that group. Regexes are culture-invariant and use a 100 ms timeout.
- File reads must tolerate another process appending to the speech file: use shared read/delete access and preserve the watcher's retry/debounce behavior. Report expected I/O, authorization, and regex-timeout failures through `ReadFailed` rather than swallowing them.
- Preserve the interaction sequence around WPF window handles: call native click-through setup only after `SourceInitialized`, defer interaction until initial positioning completes, and use the existing 50 ms interaction timer for cursor-driven state.
- `MainWindow` intentionally intercepts closing and hides instead; the tray menu's Exit action performs explicit disposal and application shutdown.
- `Assets\mascot.png` is declared as `Content` in `Baba.csproj` and copied to output. The data-directory initializer copies it to the configured mascot path only when that file does not exist.
