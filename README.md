# Zoe 🐾: a desk pet for Windows

A little Zoe who lives on the Windows desktop: walks along the taskbar and the tops of windows,
jumps up onto them, rides along when a window is dragged, falls when one closes, can be clicked
(tail wag) or picked up and thrown, and reacts to what's going on:

| situation                                    | Zoe                                     |
|----------------------------------------------|-----------------------------------------|
| a rule in `zoe.json` matches (app or tab title) | trots to the nap corner and sleeps (`nap`), sleeps where she is (`doze`), or disappears (`hidden`) |
| no keyboard/mouse for `idleDozeMinutes`      | curls up where she is; wakes when her owner's back |
| full-screen video / PowerPoint slideshow     | hidden                                  |
| screen sharing in Teams/Zoom, screenshots    | invisible to the capture (her owner still sees her) |

her owner's controls: the Zoe icon in the system tray (come here / nap time / go to bed / goodbye).

## Just want to run her?

1. Download `Zoe.zip` from the latest release (Releases, on the right of this page) and unzip it.
2. Open the unzipped `ZoeInstall` folder, click in the address bar, type `powershell` and press Enter.
   For the proper install it must be an **administrator** PowerShell: Start, type PowerShell, right-click,
   Run as administrator, then `cd` to the folder.
3. Run: `powershell -ExecutionPolicy Bypass -File install.ps1 -StartNow`

That installs her to `C:\Program Files\Zoe` for whoever is signed in and starts her at every logon.
To remove her: `powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall`.

Just trying her out? Double-click `Zoe.exe` in the unzipped folder instead: nothing is installed,
and "Goodbye for today" in her tray menu closes her.

## Layout

- `src/Zoe.Core`: behaviour state machine, physics, platform finding, rules. Pure .NET, unit tested.
- `src/Zoe.App`: Windows shell: per-pixel-alpha layered window, window enumeration, foreground
  watcher, tray icon. WinForms, single self-contained `Zoe.exe` (no .NET install needed).
- `sprites/`: 19 animations generated from photos of Zoe (see `FRAMES.md`); `gen/keys/` holds the key poses they came from.
- `zoe.json`: settings and rules, hot-reloaded.
- `deploy/install.ps1`: installs to `C:\Program Files\Zoe` + a logon scheduled task for the user.

## Build (on Linux, no SDK needed)

```bash
./build.sh         # tests + publish to dist/Zoe/
./build.sh test    # tests only
python3 tools/make_placeholder_sprites.py   # regenerate placeholder frames
```

## Deploy

Copy `dist/Zoe/` and `deploy/install.ps1` to the PC, then in an elevated PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -User Sam            # quietly install
powershell -ExecutionPolicy Bypass -File install.ps1 -User Sam -StartNow  # the reveal
```

Tuning later: edit `C:\Program Files\Zoe\zoe.json`; changes apply within a second.
Log (mood changes by rule name only, never window titles): `%LOCALAPPDATA%\Zoe\zoe.log`.
