# W75 Local

**Offline, local settings manager for the Wraith W75 V2**

An independent, unofficial Windows tool that configures the Wraith W75 V2 keyboard without a browser.

Turkish version: [README.md](README.md). The program interface is **Turkish only** for now.

## What it does

- Edits keyboard profiles, key mappings (base and Fn layer), actuation distance, Rapid Trigger, dead zones, lighting, SOCD rules and other keyboard settings.
- Writes the settings to the keyboard over USB, then reads them back to check them.
- Keeps your settings as files on your own computer (`%APPDATA%\W75Ayarlayici`). They do not depend on browser data. The data folder name `W75Ayarlayici` is kept for backward compatibility.
- Makes backups and can export and import profiles.
- Does **not** use the network. It has no update check and no telemetry.

## Supported device

- Wraith W75 V2 (`W75HEARGB`), USB VID `2E3C`, PID `C368`, wired USB.
- **Tested only with firmware V3.22.07.** Other models and firmware versions have not been tested.

## Install

1. Download `W75Local-v1.0.0.zip` from the [**Releases**](https://github.com/BaysungurLabs/W75-Local/releases) page.
2. Check the SHA256 hash (see `SHA256.txt` in the release). In PowerShell: `Get-FileHash .\W75Local-v1.0.0.zip -Algorithm SHA256`.
3. Unzip it and run `W75Local.exe`. No installation and no admin rights are needed.

Requirements: Windows 10 or 11 (64-bit; tested on Windows 11), .NET Framework 4.x (included with Windows 10/11).

The program is **not code-signed**. Windows SmartScreen may show a warning, and some antivirus programs may report a false positive. The full source code is in this repository, so you can build it yourself.

## Main features

- 4 profiles (the keyboard stores 4 profiles).
- Key mapping, Rapid Trigger, per-key or multi-key actuation, dead zones.
- 15 lighting modes with color, brightness, speed and direction, plus optional live preview.
- Polling rate 1 / 2 / 4 / 8 kHz (one setting for the whole keyboard).
- SOCD rules, Win/Alt+Tab/Alt+F4/Shift+Tab locks, N-key / 6-key mode.
- Dark theme, device backup and restore.

## Known limits

- Turkish interface only.
- Only Wraith W75 V2 with firmware V3.22.07 was tested.
- No macros, no side light strip editing, no firmware update, no wireless or gamepad modes.
- The action list has only standard keyboard keys (no media, mouse or shortcut actions).
- A 1 kHz polling setting cannot be read back from the keyboard, so it is not verified.

## How it was made

This program was developed by a user without programming experience, using AI-assisted tools such as Claude Code. The settings were checked on a real Wraith W75 V2 keyboard (firmware V3.22.07).

## Disclaimer

This is an independent community project. It has **no affiliation with Wraith or the keyboard manufacturer**. Product names and trademarks belong to their owners.

The program writes to the keyboard's firmware settings. **Use it at your own risk.** The software is provided **"AS IS"**, without any warranty. The author is not liable for data loss, device failure, loss of warranty, or problems with game accounts or services. Make a backup first and do not unplug the USB cable while writing. The program only changes keyboard settings and does not touch games, but there is no guarantee that any game or anti-cheat system accepts these settings.

See [DISCLAIMER.md](DISCLAIMER.md) (Turkish with an English summary) and the [MIT license](LICENSE).

## Build

Needs only Windows and the .NET Framework 4 C# compiler (`csc.exe`) that ships with Windows:

```powershell
powershell -ExecutionPolicy Bypass -File derle.ps1
```

To get the source: `git clone https://github.com/BaysungurLabs/W75-Local.git`. See [CONTRIBUTING.md](CONTRIBUTING.md) to contribute.

Turkish version: [README.md](README.md). Report problems on the [Issues](https://github.com/BaysungurLabs/W75-Local/issues) page.

Developer: Baysungur. My YouTube channel, where I make benchmark and mod showcase videos: [@BaysungurLabs](https://www.youtube.com/@BaysungurLabs)
