# 🎵 Resona

A modern Windows audio player built with WinUI 3 and .NET 8. Designed for a quick startup experience and excellent quality, Resona organizes your music library without getting in your way.

## ✨ Features

* 🎨 **Modern Design:** Beautiful WinUI 3 interface with support for 3 backdrop materials: Solid Color, Acrylic, and Mica.
* 🎧 **Bit-perfect Playback:** Uses NAudio (with an option to activate WASAPI Exclusive mode to bypass the Windows mixer for pure, unaltered sound).
* 🔊 **Non-Destructive Normalization:** ReplayGain-style RMS/peak volume analysis. Gain is applied only during playback, ensuring your original files are never modified.
* 📝 **Synchronized Lyrics:** Automatically fetches and caches synchronized lyrics via [LRCLib](https://lrclib.net).
* 🖼️ **Cover Art Fetching:** Automatically retrieves high-quality album covers using the iTunes Search API.
* 🏷️ **Auto-Tagging:** Automatically identifies unknown tracks using acoustic fingerprinting (Chromaprint/fpcalc) to fetch accurate metadata (artist, title) from MusicBrainz.
* 📂 **Playlist Management:** Full support for M3U and M3U8 import/export.
* 🤖 **AI DJ:** Provides prompts to AI models to generate smart playlists, suggest tracks, and offer deep insights into your music collection.
* 🌐 **Language:** Available in English and French. The interface auto-adapts to your OS language.

---

## 🗑️ Uninstall

Resona does not have an installer — to fully remove it:

1. Exit Resona (right-click the tray icon → **Quit** or close the app).
2. Delete the application folder containing the `.exe` file.
3. Delete the following cache/settings folder:
   - `%LOCALAPPDATA%\Resona`

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

## ⚠️ Disclaimer

This application was conceived and fully coded by AI. Since no other audio player on the market currently offers this blend of modern design, and all these handy features, this AI-generated solution fills the gap.

> [!NOTE]
> The day a human developer creates a similar open-source application with equivalent or superior quality, this repository will be permanently deleted.

---

## 🔒 Security & Permissions

Since this is AI-generated code, transparency is key:

* **No Administrative Privileges:** This application explicitly runs with standard user permissions (`asInvoker`). It does not require, nor will it ever ask for, Administrator privileges to run.
* **UAC Safety Indicator:** If the application ever prompts you with a Windows UAC (User Account Control) warning asking for admin rights, close it immediately—that means the binary has been altered or compromised.

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
    ├── Assets/                # Images, fonts, and static resources
    ├── Converters/            # XAML value converters (UI data binding)
    ├── Helpers/               # Utility classes (animations, UI extensions)
    ├── Models/
    │   ├── Track.cs           # Core track metadata and state (cover, lyrics, gain)
    │   ├── Playlist.cs        # Playlist entity
    │   └── Strings.cs         # Localization strings (FR/EN)
    ├── Services/
    │   ├── AudioEngineService.cs   # NAudio/WASAPI exclusive playback engine
    │   ├── NormalizationService.cs # Non-destructive audio gain analysis
    │   ├── LyricsService.cs        # LRCLib fetching and parsing
    │   ├── CoverArtService.cs      # iTunes Search API implementation
    │   ├── PlaylistM3uService.cs   # M3U/M3U8 import & export logic
    │   ├── LibraryCacheService.cs  # SQLite fast startup cache
    │   └── LibraryScannerService.cs# Background disk scanning via TagLib
    └── Views/
        ├── LibraryPage.xaml(.cs)   # Main library grid
        ├── AlbumsPage.xaml(.cs)    # Album grouping
        ├── PlaylistsPage.xaml(.cs) # Playlist management
        └── LyricsPage.xaml(.cs)    # Synchronized lyrics view
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
- `library_cache.db`: The SQLite database allowing instantaneous app launches.
- `Covers\`: A directory containing all the cached album art downloaded from the internet.
- `ai_backup.json`: Backup data for AI-generated playlists and suggestions.
