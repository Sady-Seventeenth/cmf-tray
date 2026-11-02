# CMF Tray (Enhanced Edition)

A lightweight Windows tray app to control **CMF / Nothing earbuds** — noise control, Ultra bass, battery, low-latency gaming mode, and dual-connection audio switching — right from your taskbar.

> Unofficial. Not affiliated with, or endorsed by, Nothing. Protocol reverse-engineered by the community (radiance-project/ear-web, bharadwaj-raju).

## Features & Improvements in this Fork

- 🌐 **Russian Localization**: Entire UI, system tray menus, and popups translated into Russian.
- 🔀 **Dual-Connection Audio Switch**: Easily switch the active audio stream between PC and smartphone (`💻` / `📱`) directly from the panel or tray menu without disconnecting Bluetooth.
- 🎨 **Adaptive Wallpaper Palette (Material You)**: UI dynamically samples current desktop wallpaper / Windows accent colors with acrylic blur and fluent styling.
- ⚡ **Low-Latency Mode**: Quick toggle for gaming / low-lag mode with real-time checkmark indicator in tray menu.
- 🔋 **Battery Monitoring & Alerts**:
  - Separate battery readings for Left earbud, Right earbud, and Case.
  - Windows toast warning when either earbud battery drops to $\le$ 20%.
  - Optimized 5-minute background polling to conserve PC and earbud battery.
- 🔍 **Smart Auto-Connect**: Automatically prioritizes paired CMF/Nothing earbuds on any machine without hardcoded MAC addresses or device IDs.
- 🖼️ **Custom App Icon**: Includes custom multi-resolution Windows application icon in CMF signature style.

## Download & Run

1. Download the latest `CmfTray.exe` from [**Releases**](../../releases).
2. Double-click it. (Windows SmartScreen may display a warning on first launch — click **More info → Run anyway**).
3. The app sits quietly in the system tray.
   - **Left-click** the tray icon to open the flyout control panel.
   - **Right-click** for quick actions, ANC presets, low-latency toggle, and audio routing.

## Requirements

- Windows 10 / 11 with Bluetooth.

## Build from Source

Requires .NET 8 SDK:

```bash
dotnet build -c Release
```

Publish as a standalone single-file `.exe`:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## License

MIT — see [LICENSE](LICENSE).
