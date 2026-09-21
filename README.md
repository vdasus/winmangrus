# winmangrus

by vdasus

Tray utility that saves the position and size of selected windows into named profiles and restores them later.
A rewrite of [TANWindowMgr](../TANWindowMgr-master) on .NET 10: plain Win32 API, Native AOT, a single ~2 MB
`winmangrus.exe` with no runtime dependency, plus `winmangrus.json` next to it.

Requirements: Windows 10 or 11, x64.

## Usage

- **Left click** on the tray icon opens *Manage profiles*: pick a profile (or create one with *New...*), check windows,
  click *Save checked* (or *Save all*). Windows on other virtual desktops are listed too.
- **Right click** opens the menu:
  - *Restore: &lt;profile&gt;*: move the profile's windows back. The bold entry is the profile that matches the current
    monitor layout.
  - *Auto-restore on display change / wake*: when monitors are connected, disconnected, or the PC wakes from sleep,
    restore the most recently saved profile whose monitor layout matches the current one.
  - *Start with Windows*: adds or removes `HKCU\...\CurrentVersion\Run\winmangrus`.

Windows are matched by executable name and window class. An exact title match is preferred, so several windows of the
same program (browsers, terminals) are restored individually when their titles did not change.

## Command line

Same switches as TANWindowMgr (`-` or `/` prefix):

| Switch | Action |
| --- | --- |
| `-saveall` | Save all open windows to the `Default` profile |
| `-restore` | Restore the `Default` profile |
| `-restore=<Name>` | Restore profile `<Name>` (use quotes if it contains spaces) |
| `-autoexit` | Do not stay in the tray after processing the switches |
| `-h`, `--help` | Show the list of switches |

Exit code is `1` if a requested profile does not exist, `2` on an unknown switch.

```
winmangrus.exe -restore="Home layout" -autoexit
```

## Settings file

`winmangrus.json` is stored next to the executable. Each profile stores the monitor layout at save time and, per window,
`exe`, `class`, `title`, screen rectangle, and state (`Normal`, `Maximized`, `Minimized`). If the file cannot be parsed,
it is copied to `winmangrus.json.bad` and an empty configuration is used.

The old `Settings.ini` format of TANWindowMgr is not imported.

## Known limitations

- Windows of elevated (administrator) processes cannot be moved unless winmangrus runs elevated as well.
- Minimized UWP apps are not listed (Windows cloaks them).

## Build

The version is stored in `version.txt`; every `dotnet publish` increments the patch number and writes it back.
Pass `-p:NoVersionBump=true` to publish without incrementing.

Requires .NET SDK 10 and the Visual Studio "Desktop development with C++" workload (Native AOT linker).

```
dotnet publish -c Release -o publish
```

If only MSVC `link.exe` is installed without the full workload and the Windows SDK, pass the MSVC tools folder; the
Windows SDK import libraries are then downloaded from NuGet (`Microsoft.Windows.SDK.CPP.x64`):

```
dotnet publish -c Release -o publish -p:MsvcDir="C:\Program Files\Microsoft Visual Studio\18\Professional\VC\Tools\MSVC\14.51.36231"
```
