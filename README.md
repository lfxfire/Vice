# Vice

Vice is a Windows tray app that drives a TV and soundbar through an Arduino IR transmitter on a serial port, and controls the PC itself (lock, sleep, media keys, volume). It takes commands from three places:

- the buttons in its own window
- a Dropbox-synced command file (for voice assistants and IFTTT)
- the **Vice Remote** Android app, over Tailscale (see below)

All three go through `MainVM.ExecuteCommand`, so they understand the same commands.

## Phone control with Vice Remote

```
Vice Remote (Android)  --Tailscale (encrypted)-->  Vice on the PC, port 8420  -->  Arduino  -->  TV / soundbar
```

Tailscale gives the PC a private address that works from home Wi-Fi and from mobile data, without opening anything on your router. Vice only answers requests that carry its pairing code, and the firewall rule Vice adds only lets Tailscale addresses in.

### One-time setup

1. Install [Tailscale](https://tailscale.com/download) on the PC and on your phone, and sign in to the same account on both.
2. Build and run Vice. On first start it asks for a one-time Windows permission (a UAC prompt) so it can listen on port 8420 and open the firewall for Tailscale. If you skipped it, use **Phone > Allow phone access (admin)**.
3. In Vice, open **Phone > Show pairing details**. It shows the PC address (the PC's name in Tailscale), the port and a pairing code.
4. Install Vice Remote on the phone (see [ViceRemote/README.md](ViceRemote/README.md)), and enter those three details.

The Phone menu can also copy the pairing code, turn the phone API off, or make a new code (the old one stops working straight away).

If the PC's name doesn't work as the address, use its `100.x.y.z` address from the Tailscale app instead.

### Settings

These live in `%LocalAppData%\Vice\Data.txt` next to the other settings:

| Setting | Default | Meaning |
|---|---|---|
| `PhoneApiEnabled` | `true` | Turns the phone API on or off |
| `PhoneApiPort` | `8420` | Port Vice listens on. After changing it, run **Phone > Allow phone access** again |
| `PhoneApiToken` | made on first run | The pairing code |

### API

Every `/api` request needs the header `Authorization: Bearer <pairing code>`. Dashes, spaces and case in the code are ignored.

| Request | Reply |
|---|---|
| `GET /` | `Vice is running` (no code needed, for testing the connection) |
| `GET /api/state` | `{ "ok": true, "version": "1", "state": { ... } }` |
| `POST /api/command` with `{ "command": "...", "value": "...", "value2": "..." }` | `{ "ok": true/false, "error": "...", "state": { ... } }` |

`state` holds `tvPower`, `barPower`, `tvVolume`, `tvTargetVolume`, `wooferVolume`, `wooferTargetVolume`, `tvMode`, `tvModeChanging`, `nightMode`, `sleepTimerActive`, `sleepTimerMinutes`, `sleepTimerAction`, `sendingBlocked` and `dropboxPolling`.

For example, from a PC on the same tailnet:

```
curl -H "Authorization: Bearer ABCD-EFGH-JKMN-PQRS-TUVW" -d "{\"command\":\"TV Volume\",\"value\":\"Up 2\"}" http://my-pc:8420/api/command
```

## Commands

`command`, `value` and `value2` are the same as lines 1, 2 and 3 of the Dropbox command file.

| command | value | value2 | What it does |
|---|---|---|---|
| `Test` | | | Logs only |
| `Lock`, `Sleep`, `Hibernate`, `PowerOff` | | | PC power. `PowerOff` shuts down after 2 seconds |
| `Volume Control` | `Mute`, `Up 10`, `Down 10` or a number | | PC volume |
| `Media Control` | `Next`, `Previous`, `Play/Pause` | | Media keys |
| `Type` | `Left 3`, `Right 3`, `Type some words` | | Arrow keys or typing |
| `Remote` | `Power` | `TV` or `Bar` | Toggles TV or soundbar power |
| `Remote` | `NightMode` | | Toggles soundbar night mode |
| `Remote` | `Volume` / `Woofer` | `Up 3`, `Down 3` | Sends that many IR steps |
| `Remote` | `Tv Mode` | `Up`, `Down`, `UpW`, `DownW` | Steps the TV picture mode |
| `Remote` | `Bed time` | blank, or minutes | Blank: TV off then the sleep timer's action. Minutes: starts the sleep timer |
| `TV Volume` | `Up 2`, `Down 2`, `Set 30` | | Moves the soundbar volume target (0 to 50) |
| `Woofer Volume` | `Up 2`, `Down 2`, `Set 6` | | Moves the woofer volume target (0 to 12) |
| `TV Mode` | `Normal`, `Cinema`, `True Cinema` | | Switches picture mode |
| `Sleep Timer` | `Add 5`, `Set 30`, `Start`, `Stop` | | Sleep timer |

Numbers can be digits or words up to nineteen ("five"), for voice assistants. An unknown command is logged and rejected instead of being ignored, and so is a value Vice can't read, such as a blank volume, rather than being treated as 0.

## Building

Open `Vice.sln` in Visual Studio on Windows (.NET Framework 4.7.2). Vice is Windows-only.

## Installing

Vice installs with ClickOnce from a folder on this PC, `D:\Storage\Public\Vice` (`PublishUrl` in `Vice.csproj`). The installed copy checks that folder for a newer version each time it starts.

1. If Settings > Apps > Installed apps lists an old **Vice - 1**, uninstall it. It's a 2020 build that updates from the PC's old network name, so it never updates.
2. Double-click `Publish.cmd`. It builds the installer into the folder above and moves `Vice.csproj` on to the next version.
3. Run `D:\Storage\Public\Vice\setup.exe` and choose **Install**. Windows says the publisher can't be verified because the installer isn't signed.

To update, publish again and restart Vice.

To publish from Visual Studio instead, use **Project > Properties > Publish > Publish Now**. Avoid the Publish Wizard: it turns manifest signing back on, and an installed copy refuses an update signed differently from itself.
