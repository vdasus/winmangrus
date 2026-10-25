# winmangrus

by vdasus

Lightweight Windows window layout manager. Save the position and size of your windows into named profiles and restore
them with one click, or automatically when monitors are connected or disconnected, or the PC wakes from sleep. Useful
with multiple monitors, docking stations, and remote desktop sessions, where Windows tends to move windows around.

- Single ~2 MB `winmangrus.exe`, no installation, no .NET runtime required (Native AOT), ~12 MB RAM in the tray.
- Settings in `winmangrus.json` next to the executable (portable).
- Restores several windows of the same program individually; supports virtual desktops, per-monitor DPI, and
  maximized or minimized windows.
- Command-line switches for scripts and scheduled tasks.

A rewrite of [TANWindowMgr](https://github.com/Todd1561/TANWindowMgr) by Todd Nelson on .NET 10 with the plain Win32 API.

Requirements: Windows 10 or 11, x64.

## Download

Get `winmangrus.exe` from [Releases](https://github.com/vdasus/winmangrus/releases), put it in any folder, and run it.
Enable *Start with Windows* in the tray menu if needed.

If your network blocks downloading `.exe` files, take `winmangrus-<version>-win-x64.zip` from the same release instead.
It contains `winmangrus.exe`, `VirtualDesktopAccessor.dll` and both licenses; unpack it into any folder.

For the *Desktop hotkeys*, also download `VirtualDesktopAccessor.dll` from the same release and put it in the same
folder as `winmangrus.exe`. Without it, everything else works; only the hotkeys cannot be enabled.

## Usage

- **Left click** on the tray icon opens *Manage profiles*: pick a profile (or create one with *New...*), check windows,
  click *Save checked* (or *Save all*). Windows on other virtual desktops are listed too.
- **Right click** opens the menu:
  - *Restore: &lt;profile&gt;*: move the profile's windows back. The bold entry is the profile that matches the current
    monitor layout.
  - *Auto-restore on display change / wake*: when monitors are connected, disconnected, or the PC wakes from sleep,
    restore the most recently saved profile whose monitor layout matches the current one.
  - *Start with Windows*: adds or removes `HKCU\...\CurrentVersion\Run\winmangrus`.
  - *Desktop hotkeys*: enable or disable `Ctrl+1` ... `Ctrl+9` one by one; each switches to that virtual desktop.
    All are disabled by default. Requires `VirtualDesktopAccessor.dll` next to `winmangrus.exe`; `dotnet publish`
    copies it there from `lib/` (see [Third-party components](#third-party-components)). An enabled hotkey is taken
    globally, so applications no longer receive it (for example, `Ctrl+1` for browser tabs). If another program (such
    as AutoHotkey) already holds the key, enabling it fails with a message.
  - *Releases page...*: opens the GitHub releases page in the browser to check for a newer version.

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

The publish folder then contains `winmangrus.exe`, `VirtualDesktopAccessor.dll`, the licenses, and
`winmangrus-<version>-win-x64.zip` with all of them, ready to attach to a GitHub release.

### Release

Pushing a tag `v<version>` runs the `Release` GitHub Actions workflow: it builds on `windows-latest` and attaches
`winmangrus.exe`, `VirtualDesktopAccessor.dll` and the zip to the GitHub release of that tag, creating the release with
generated notes if it does not exist yet. The tag must match `version.txt`, otherwise the workflow fails.

```
echo 1.2.1 > version.txt
git commit -am "chore: bump version to 1.2.1"
git tag v1.2.1
git push origin main v1.2.1
```

If only MSVC `link.exe` is installed without the full workload and the Windows SDK, pass the MSVC tools folder; the
Windows SDK import libraries are then downloaded from NuGet (`Microsoft.Windows.SDK.CPP.x64`):

```
dotnet publish -c Release -o publish -p:MsvcDir="C:\Program Files\Microsoft Visual Studio\18\Professional\VC\Tools\MSVC\14.51.36231"
```

## Third-party components

`lib/VirtualDesktopAccessor.dll` is a copy of [VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor)
by Jari Pennanen (Ciantic), release `2024-12-16-windows11`, distributed under the MIT license
(`lib/VirtualDesktopAccessor-LICENSE.txt`). It is stored in the repository so that builds do not depend on a download.
SHA-256: `8740c572a1c000e3b87ffeb1e4c397eae9af3bd4a2abdc3bcffacab4493f8ff5`.

The DLL relies on undocumented Windows interfaces that can change between Windows builds. If the desktop hotkeys stop
working after a major Windows update, replace the file with a newer release.

## Authorship

The code was written by [Claude](https://claude.com/claude-code) (Anthropic) under the supervision of vdasus, who defined
the requirements, reviewed, and tested the result.

## Disclaimer

This software is provided "as is", without warranty of any kind, express or implied. The author accepts no
responsibility or liability for any damage, data loss, or other consequences arising from its use. Use it at your own
risk. See [LICENSE](LICENSE).
