# CMF Tray

A tiny Windows tray app to control **CMF / Nothing earbuds** — noise control, Ultra bass, and
battery — right from your taskbar, without the phone app. It talks to the earbuds' Bluetooth
control channel (the same one the official Nothing X app uses).

<!-- Add a screenshot here after your first release: ![screenshot](docs/screenshot.png) -->

> Unofficial. Not affiliated with, or endorsed by, Nothing. Protocol reverse-engineered by the
> community (radiance-project/ear-web, bharadwaj-raju).

## Download & run (for everyone)

1. Go to the [**Releases**](../../releases) page and download the latest `CmfTray.exe`.
2. Double-click it. **Windows SmartScreen** may show a blue "Windows protected your PC" box —
   this is normal for unsigned indie apps. Click **More info → Run anyway**.
3. A tray icon appears (bottom-right, maybe under the `^` arrow).
4. **Left-click** the icon to open the control panel; **right-click** for the menu.

There's nothing to install and no .NET required — the app is self-contained.

### First-time setup
- Your earbuds must be **paired** in Windows (Settings → Bluetooth) and **connected to this PC**
  (not your phone — CMF/Nothing buds connect to one device at a time unless multipoint is on).
- Turn on **Start with Windows** from the right-click menu so it launches automatically.

## Features

- **Noise control** — Noise cancellation / Transparency / Off
- **Ultra bass** — on/off + level
- **Battery** — left and right earbud
- Live updates — changing the mode on the earbuds reflects in the panel automatically
- Auto-reconnect when the buds come back to this PC
- Optional launch at login

## Supported earbuds

Built and tested against **CMF Buds**. Should also work with other Nothing/CMF models that use
the same protocol (Ear, Ear (a), CMF Buds Pro, …), though some may need small tweaks. If yours
behaves oddly, open an issue with your model and firmware version.

## Requirements

- Windows 10/11 with Bluetooth

## Build from source (for developers)

Requires the .NET 8 SDK.

```bash
dotnet build -c Release
```

Publish the self-contained single-file exe that ships in Releases:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

### Diagnostic modes
- `CmfTray.exe --probe` — lists paired devices, connects, and dumps battery/firmware/ANC responses
- `CmfTray.exe --anc-map` — maps ANC mode command bytes against your hardware
- `CmfTray.exe --shot` — renders the panel to `flyout-shot.png`

## License

MIT — see [LICENSE](LICENSE).
