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

### Setup, step by step

You do this once. Allow about an hour, most of it waiting for downloads. You need:

- Vice installed and running on the PC (see [Installing](#installing))
- an Android phone with Android 8.0 or newer. There is no iPhone version
- a USB cable that carries data, not only power
- a Tailscale account, which is free for personal use

There are four parts: let Vice accept the phone, link the phone and the PC with Tailscale, put the app on the phone, then give the app Vice's details. Each part ends with a check, so you know it worked before moving on.

#### Part 1: let Vice accept the phone

1. Start Vice. The first time, it asks "Vice needs a one-time Windows permission so your phone can reach it over Tailscale. Allow it now?" Choose **Yes**, then **Yes** again on the Windows prompt that follows. If you chose No, or never saw the question, use **Phone > Allow phone access (admin)** in Vice's menu instead.
2. Open **Phone > Show pairing details**.

**Check:** the last line of the box says `Phone API: Running`. The box also lists the PC address, port and pairing code you need in Part 4.

#### Part 2: link the phone and the PC with Tailscale

Tailscale is an app that gives your own devices a private, encrypted connection to each other, so the phone can reach the PC from home Wi-Fi or from mobile data. The phone can't connect without it, because Vice's firewall rule only lets Tailscale addresses in.

1. On the PC, install Tailscale from [tailscale.com/download](https://tailscale.com/download) and sign in. It then sits as an icon next to the clock.
2. On the phone, install **Tailscale** from the Play Store and sign in to the same account. When Android asks to allow a VPN connection, tap **OK**.
3. In the Tailscale app on the phone, find the PC in the list of devices. Note its name and its address, which looks like `100.x.y.z`.

**Check:** in the phone's browser, open `http://PC-NAME:8420/` with the PC's name from step 3, typing the `http://` too. The page should say `Vice is running`. If it doesn't load, try the address instead: `http://100.x.y.z:8420/`. Whichever one works is your **PC address** in Part 4.

#### Part 3: put Vice Remote on the phone

Vice Remote isn't in the Play Store. Android Studio, a free program from Google, builds it on the PC and copies it to the phone through the USB cable. Once it's on the phone, it runs without the cable.

1. Install [Android Studio](https://developer.android.com/studio) and start it. In its setup wizard choose **Standard** and accept the licences. It downloads several GB of Android tools.
2. Choose **Open** (or **File > Open**), pick the `ViceRemote` folder inside this repository, and choose **Trust Project**. Pick `ViceRemote` itself, not the `Vice` folder above it.
3. Wait for the sync to finish, shown by the progress bar at the bottom right. The first time takes several minutes. If Android Studio suggests upgrading the Android Gradle plugin, ignore it.
4. On the phone, turn on USB debugging. The names vary a little between phone makers:
   - Open **Settings > About phone** and tap **Build number** seven times, until it says you are a developer. On Samsung phones, Build number is under **Software information**.
   - Open **Settings > System > Developer options** (on Samsung, **Settings > Developer options**) and switch on **USB debugging**.
5. Plug the phone into the PC and unlock it. When it asks "Allow USB debugging?", tick **Always allow from this computer** and tap **Allow**.
6. In Android Studio, check that the box next to the green ▶ button at the top shows your phone, then press ▶. The first build takes a few minutes.

**Check:** Vice Remote opens on the phone and shows **Connect to your PC**. It's now installed like any other app, and you can unplug the cable.

#### Part 4: give the app Vice's details

1. On the PC, open **Phone > Show pairing details** in Vice.
2. On the phone, fill in **Connect to your PC**:
   - **PC address**: the name or `100.x.y.z` address that worked in Part 2
   - **Port**: `8420`, which is already filled in
   - **Pairing code**: as shown on the PC. Dashes and capitals don't matter
3. Tap **Save and connect**.

**Check:** "Connecting to your PC…" disappears, and **Volume** in the app shows the same number as **Bar Volume** in Vice on the PC.

To change the details later, tap the cog at the top right of the app. Vice's Phone menu can also copy the pairing code, turn the phone API off, or make a new code (the old one stops working straight away).

#### If it doesn't work

**The browser check in Part 2 fails, or the app says "Can't reach the PC" or "The PC didn't answer in time"**

- Open Tailscale on the PC and on the phone and check both are connected.
- Check Vice is running and the PC is awake. The phone can't wake a sleeping PC.
- Check **Phone > Show pairing details** says `Phone API: Running`. If it doesn't, use **Phone > Allow phone access (admin)**.
- Use the PC's `100.x.y.z` address instead of its name.
- Turn off any other VPN. Android runs only one VPN at a time, so another VPN app on the phone switches Tailscale off, and a VPN on the PC can block Tailscale.

**The app says "Wrong pairing code"**

Type the code again from **Phone > Show pairing details**. If you chose **New pairing code**, the old one no longer works.

**The app says "Something answered on that port, but it isn't Vice"**

The PC address or port points at something else. Check both against the pairing details.

**It worked for months, then stopped**

By default Tailscale signs a device out after 180 days. Open Tailscale on the PC and on the phone and sign in again. To stop it happening to the PC, turn off key expiry for it in Tailscale's admin console.

**The phone doesn't appear in Android Studio**

- Unlock the phone and look for the "Allow USB debugging?" question.
- Try another cable or USB port. Some cables only charge.
- Pull down the phone's notifications, tap the USB one and choose **File transfer**.
- Some phones need their maker's USB driver installed on Windows.

**The sync or build fails in Android Studio**

Check you opened the `ViceRemote` folder itself, and that the PC is online, because the first sync downloads its build tools.

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
