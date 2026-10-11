# Quode Palette Capture

A simple screen recorder for Windows. It records your screen and your system audio, and nothing else. No webcam, no microphone, no flicker.

## What it does

- Records any monitor you pick, saved as a standard `.mp4`
- Can draw a blue ring around the mouse pointer so viewers can follow it, with almost no performance cost
- Captures system audio, so whatever you hear through speakers, wired headphones or Bluetooth ends up in the recording
- Keeps audio and video in sync
- Plays back at real speed, with no slow motion
- Uses little CPU when your GPU supports hardware H.264 encoding
- Has a dark interface with Montserrat built in, so it looks the same on every machine
- Never touches your microphone or camera

## How it works

| Goal | Approach |
|---|---|
| No flicker or tearing | Frames come from the Windows Graphics Capture API, the same GPU pipeline Windows itself uses |
| Real-speed video | Every frame carries a timestamp from the capture API, so the video cannot drift |
| Synced audio | Audio and video are muxed against one shared clock while recording, not joined afterward |
| System audio only | WASAPI loopback on your default playback device, whatever it is |
| Light on resources | Hardware H.264 encoding through Media Foundation, with Low, Medium and High presets |

Capture and encoding are handled by [ScreenRecorderLib](https://www.nuget.org/packages/ScreenRecorderLib).

## Requirements

- Windows 10 (version 1903 or later) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), only needed to build
- An internet connection for the first build, so NuGet can download the recording library

The finished app is self-contained, so the machine that runs it does not need .NET installed.

## Build

1. Clone or download this repository.
2. Double-click `build.bat`.
3. Open the `dist` folder and run `Quode Palette Capture.exe`.

Copy the whole `dist` folder if you want to move the app, not just the exe. ScreenRecorderLib contains native code that can't be bundled into a single file, so the app ships as a folder.

You can also build from the command line:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:Platform=x64 -o dist
```

## Using it

1. Choose a monitor, a quality preset, whether to include system audio, and whether to highlight the mouse pointer.
2. Pick a save folder if you don't want the default, `Videos\Quode Palette Capture`. Folders inside your user profile are shown as `..\Videos\...` in the window, so your Windows username stays off screen when you record tutorials or videos.
3. Click **Start Recording**. Use **Pause** and **Resume** when you need to, then **Stop**.

Recordings are saved as `QuodePaletteCapture_yyyyMMdd_HHmmss.mp4`.

Closing the window sends the app to the system tray instead of quitting, so a click in the wrong place can't end a recording. Right-click the tray icon for Start, Pause and Stop, or choose **Exit** to close the app completely.

## Project layout

```
QuodePaletteCapture.csproj   Project file and package reference
Program.cs            Entry point and error handling
MainForm.cs           Interface, tray icon and recording logic
CursorHighlight.cs    Blue ring that follows the mouse pointer
AppFonts.cs           Loads the embedded Montserrat fonts
AppTheme.cs           Dark theme, title bar and custom controls
Fonts/                Montserrat (SIL Open Font License)
AppIcon.ico / .png    App icon
build.bat             One-click build script
```

## Troubleshooting

**Package restore fails.** The first build needs internet access to download ScreenRecorderLib. After that it is cached.

**The build complains about ScreenRecorderLib.** The project is pinned to version `6.6.0`. If you change it on purpose and something breaks, go back to `6.6.0` or check the [release notes](https://www.nuget.org/packages/ScreenRecorderLib).

**Recording won't start.** This is usually a graphics driver issue. Update your driver. Without hardware encoding the app still works, with a bit more CPU load.

**The recording is black.** Protected content such as DRM video can't be captured, and neither can a Remote Desktop session. Record the physical screen locally instead.

**Some dialogs aren't dark.** The folder picker and message boxes belong to Windows and keep the system theme. Everything else is themed.

## License

Add your preferred license for the app code here. Montserrat is included under the SIL Open Font License, see `Fonts/OFL.txt`.
