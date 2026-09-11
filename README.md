# Now Playing

`NowPlaying.exe` is a compact transparent Windows desktop widget. Only the
album art, track text, and individual controls have visible surfaces. It shows
the artwork and a single `track — artist` line from the currently active Windows
media session, refreshing every two seconds. It does not use a Spotify account,
web API, background service, or network connection.

## Run

Double-click `NowPlaying.exe`, then play something in Spotify, Apple Music, or
another supported media app. The widget reads Windows' active media session, so
it needs Windows 10 version 1809 or later.

Drag it from anywhere to reposition it. Press `Esc` or `Alt` + `F4` to quit.
It uses SF Pro Display/Text when that Apple font is installed; otherwise it
falls back to Segoe UI without bundling or installing any font.

Use the `⏮`, play/pause, and `⏭` controls to navigate playback. The small `−`
minimizes the widget and `×` exits it.

It is a 10 KB .NET Framework desktop executable and uses the Windows media
session API that ships with Windows. There is no installer or third-party
runtime to install on standard Windows 10/11 PCs.

## Build from source

The included executable is ready to use. To rebuild it, run `build.ps1` from a
Developer PowerShell on a Windows computer with the Windows 10/11 SDK installed.
The build has no package restore or external dependencies.

## Notes

- It only reads playback metadata. It does not control Spotify or collect data.
- If Spotify is not open, it shows a quiet waiting state.
- Closing the window exits the app.

## DISCLAIMER

- This app wasn't and won't be affiliated by spotify / apple / any music streaming app
