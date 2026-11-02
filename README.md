# CMF Tray (Enhanced Edition)

[🇷🇺 Описание на русском](#-описание-на-русском) | [🇬🇧 English Description](#-english-description)

---

## 🇷🇺 Описание на русском

Компактная утилита для системного трея Windows для полноценного управления наушниками **CMF by Nothing** (CMF Buds, Buds Pro, Buds Pro 2, Nothing Ear и др.) без необходимости открывать мобильное приложение. 

Приложение подключается напрямую по Bluetooth к каналу управления наушников (RFCOMM), аналогично официальному приложению Nothing X.

### ✨ Возможности и улучшения в этом форке:

- 🌐 **Полная русская локализация**: весь интерфейс всплывающей карточки, системные подсказки и контекстное меню трея переведены на русский язык.
- 🔀 **Мгновенное переключение звука (Dual Connection)**: 
  - Решает проблему, когда при одновременном подключении к ноутбуку и телефону фоновый звук с компьютера (игра, видео, браузер) не даёт наушникам воспроизвести звук с телефона.
  - По нажатию кнопки `📱 Отдать звук телефону` компьютерный звук мгновенно глушится на системном уровне, и наушники гладко переходят на воспроизведение с телефона.
  - По нажатию `💻 Вернуть звук на ноутбук` звук мгновенно возвращается.
  - Текущий источник звука наглядно отображается иконками: `Источник звука: 💻` или `Источник звука: 📱`.
- 🎨 **Адаптивный интерфейс под обои (Material You)**:
  - Приложение автоматически анализирует текущие обои рабочего стола (или системный акцентный цвет Windows) и окрашивает плашку и элементы управления в гармоничный акриловый оттенок с размытием.
- ⚡ **Режим низкой задержки (Low-Lag / Gaming Mode)**:
  - Быстрое включение и выключение режима с низкой задержкой прямо из контекстного меню с отображением статуса галочкой `✓`.
- 🔋 **Мониторинг батареи и оповещения**:
  - Раздельный показ уровня заряда для левого наушника, правого наушника и кейса.
  - Всплывающее уведомление Windows, когда любой из наушников разряжается до 20% (напоминание убрать в кейс).
  - Опрос заряда оптимизирован до одного раза в 5 минут, чтобы не нагружать процессор и не сажать батарею наушников.
- 🔍 **Умное автоподключение**:
  - При запуске приложение само находит спаренные наушники CMF/Nothing с приоритетом над смартфонами и подключается без необходимости прописывать серийные номера или MAC-адреса. Работает на любом ПК из коробки.
- 🖼️ **Фирменная иконка CMF**:
  - В исполняемый файл встроена качественная векторная иконка в фирменном оранжевом стиле CMF by Nothing (поддерживает все размеры от 16x16 до 256x256 в Windows 11).

---

### 🚀 Скачать и запустить

1. Перейдите на страницу **[Releases](../../releases)** и скачайте файл `CmfTray.exe`.
2. Запустите его (при первом запуске Windows SmartScreen может показать предупреждение — нажмите *«Подробнее»* → *«Выполнить в любом случае»*).
3. В правом нижнем углу появится значок наушников:
   - **Левый клик** — открыть компактную панель управления (шумоподавление, бас, батарея, переключатель звука).
   - **Правый клик** — контекстное меню с режимами, низкой задержкой и выходом.
   - Опция **«Запускать вместе с Windows»** позволяет настроить автозапуск при входе в систему.

---

### 🛠️ Сборка из исходников

Требуется **.NET 8 SDK**:

```bash
# Обычная сборка
dotnet build -c Release

# Сборка в единый автономный exe-файл без внешних зависимостей
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

---

## 🇬🇧 English Description

A lightweight Windows tray app to control **CMF / Nothing earbuds** — noise control, Ultra bass, battery, low-latency gaming mode, and dual-connection audio switching — right from your taskbar without using the phone app.

> Unofficial. Not affiliated with, or endorsed by, Nothing. Protocol reverse-engineered by the community (radiance-project/ear-web, bharadwaj-raju).

### Features & Improvements in this Fork

- 🌐 **Russian Localization**: Entire UI, system tray menus, and popups translated into Russian.
- 🔀 **Dual-Connection Audio Switch**: Seamlessly switch the active audio stream between PC and smartphone (`💻` / `📱`) directly from the panel or tray menu without disconnecting Bluetooth.
- 🎨 **Adaptive Wallpaper Palette (Material You)**: Dynamically extracts current desktop wallpaper / Windows accent colors with fluent acrylic blur.
- ⚡ **Low-Latency Mode**: Quick toggle for gaming / low-lag mode with real-time checkmark indicator in tray menu.
- 🔋 **Battery Monitoring & Alerts**:
  - Separate battery readings for Left earbud, Right earbud, and Case.
  - Windows toast warning when either earbud battery drops to $\le$ 20%.
  - Optimized 5-minute background polling to conserve PC and earbud battery.
- 🔍 **Smart Auto-Connect**: Automatically prioritizes paired CMF/Nothing earbuds on any machine without hardcoded MAC addresses or device IDs.
- 🖼️ **Custom App Icon**: Includes custom multi-resolution Windows application icon in CMF signature style.

### Download & Run

1. Download `CmfTray.exe` from **[Releases](../../releases)**.
2. Run it (click *More info → Run anyway* if Windows SmartScreen pops up).
3. **Left-click** tray icon to open the flyout card; **Right-click** for menu.

### License

MIT — see [LICENSE](LICENSE).
