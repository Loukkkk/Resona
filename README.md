# 🎵 Resona

A modern Windows local music player designed for seamless library management, music downloading, auto-tagging, lyrics & cover fetching, AI playlist prompts, an equalizer, EN/FR support & more.

## ✨ Features

* 🎨 **Modern Design:** Beautiful WinUI 3 interface with support for 3 backdrop materials: Solid Color, Acrylic, and Mica.
* 🎧 **Bit-perfect Playback:** Uses NAudio (with an option to activate WASAPI Exclusive mode to bypass the Windows mixer for pure, unaltered sound).
* 🎛️ **Audio Processing:** Built-in 10-band graphic equalizer with presets to fine-tune your audio experience, and seamless crossfading between tracks.
* 🖼️ **Mini Player:** A compact, always-on-top mini player mode for controlling playback while you work.
* 🔊 **Normalization:** ReplayGain-style RMS/peak volume analysis. Gain is applied only during playback, ensuring your original files are never modified.
* 🎤 **Synchronized Lyrics:** Automatically fetches and caches synchronized lyrics via [LRCLib](https://lrclib.net), complete with a built-in translation option.
* 🖼️ **Cover Art Fetching:** Automatically retrieves high-quality covers using the iTunes Search API.
* 📖 **Artist Biographies:** Automatically fetches and displays artist biographies and information directly from Wikipedia.
* 🔍 **Auto-Tagging:** Automatically identifies unknown tracks using acoustic fingerprinting (Chromaprint/fpcalc) to fetch accurate metadata (artist, title) from MusicBrainz.
* 📝 **Playlist Management:** Full support for M3U and M3U8 import/export.
* 🎮 **Discord Rich Presence:** Automatically showcase the track you are currently listening to on your Discord profile.
* 🤖 **AI DJ:** Provides prompts to AI models to generate smart playlists, suggest tracks, and offer deep insights into your music collection.
* 🌍 **Language:** Available in English and French. The interface auto-adapts to your OS language.

---

## ⚠️ Disclaimer

This application was fully coded by AI. Since no other audio player on the market currently offers this blend of modern design, and all these handy features, this AI-generated solution fills the gap.

> [!NOTE]
> The day a human developer creates a similar open-source application with equivalent or superior quality, this repository will be permanently deleted.

---

## 🔒 Security & Permissions

Since this is AI-generated code, transparency is key:

* **No Administrative Privileges:** This application explicitly runs with standard user permissions (`asInvoker`). It does not require, nor will it ever ask for, Administrator privileges to run.
* **UAC Safety Indicator:** If the application ever prompts you with a Windows UAC (User Account Control) warning asking for admin rights, close it immediately—that means the binary has been altered or compromised.

---

## 🛠️ Built With

* **C# / .NET 8**
* **WinUI 3 (Windows App SDK)**
* **NAudio** - Core audio playback engine
* **TagLibSharp** - Metadata extraction
* **SQLite** - Local library caching

---

## 🚀 How to Build and Run

1. Open `Resona.csproj` (or the solution file) in **Visual Studio**.
2. Ensure you have the **.NET Desktop Development** workload and **Windows App SDK** component installed.
3. NuGet packages will restore automatically.
4. Select the target platform (e.g., `x64`).
5. Press **F5** to build and launch!

---

## 📂 File Structure

```
Resona/
├── .github/                   # GitHub Actions workflows for automated releases
├── .gitignore                 # Standard Visual Studio gitignore
├── README.md                  # Project documentation
└── src/
    ├── Resona.csproj          # The WinUI 3 project file
    ├── app.manifest           # Windows application manifest (permissions, DPI)
    ├── icon.ico               # Application icon
    ├── Program.cs             # Native entry point for WinUI 3
    ├── App.xaml(.cs)          # Application lifecycle and service registration
    ├── MainWindow.xaml(.cs)   # Main UI, navigation, and playback bar overlay
    ├── Converters/            # XAML value converters (UI data binding)
    ├── Helpers/               # Utility classes (animations, UI extensions)
    ├── Models/
    │   ├── Models.cs          # Core track metadata, playlist entity, settings
    │   └── Strings.cs         # Localization strings (FR/EN)
    ├── Services/
    │   ├── Services.cs              # Centralized services (AudioEngine, LibraryCache, CoverArt, AutoTag, Playlist...)
    │   ├── AIService.cs             # AI interaction and API endpoints for smart playlists
    │   ├── DiscordRpcService.cs     # Discord Rich Presence integration
    │   ├── TrayIconService.cs       # Windows system tray integration
    │   └── BackupService.cs         # AI backup data management
    └── Views/
        ├── LibraryPage.xaml(.cs)    # Main library grid
        ├── AlbumsPage.xaml(.cs)     # Album grouping
        ├── PlaylistsPage.xaml(.cs)  # Playlist management
        ├── SettingsPage.xaml(.cs)   # Application settings
        └── QueuePage.xaml(.cs)      # Up next queue and manual reordering
```

---

## 📁 Cache & Application Data Locations

Resona stores its configuration, library cache, and downloaded assets inside your Windows user local directory.

* Main Application Directory:
```
%LOCALAPPDATA%\Resona\
```
*(Equivalent to: `C:\Users\<YourUsername>\AppData\Local\Resona\`)*

Inside this folder, you will find:
- `library_cache.db`: The SQLite database that allows instantaneous app launches. It caches your library's audio metadata, custom user playlists, and all fetched synchronized lyrics to avoid redundant network requests.
- `Covers\`: A directory containing all the cached album art. Resona generates unique album hashes to ensure only one image is saved per album (instead of one per track), saving significant disk space.
- `settings.json`: Your saved preferences, UI states, and configuration (language, theme, equalizer presets, etc.).
- `ai_backup.json`: Backup data for AI-generated playlists and insights.
- `fpcalc\`: Contains the Chromaprint fingerprinting executable used for the auto-tagging feature.
---

## 🗑️ Uninstall

Resona does not have an installer — to fully remove it:

1. Exit Resona (right-click the tray icon → **Quit** or close the app).
2. Delete the application folder containing the `.exe` file.
3. Delete the following cache/settings folder:
   - `%LOCALAPPDATA%\Resona`