using System;
using System.IO;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Resona.Services;

namespace Resona;

public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindowInstance { get; private set; }
    public static TrayIconService? TrayIcon { get; private set; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    public static string? NowPlayingId { get; set; }
    public static string? NowPlayingFilePath { get; set; }

    // Services partages, instancies une seule fois (singletons legers)
    public static AudioEngineService AudioEngine { get; private set; } = null!;
    public static NormalizationService Normalization { get; private set; } = null!;
    public static LyricsService Lyrics { get; private set; } = null!;
    public static CoverArtService CoverArt { get; private set; } = null!;
    public static PlaylistM3uService PlaylistIO { get; private set; } = null!;
    public static LibraryCacheService Cache { get; private set; } = null!;
    public static LibraryScannerService Scanner { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;
    public static PlayStatsService PlayStats { get; private set; } = new();

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // --- Demarrage en deux temps pour un lancement percu instantane ---
        // 1) On ouvre la fenetre TOUT DE SUITE avec les donnees deja en cache SQLite
        // 2) Le scan disque / re-analyse tourne ensuite en tache de fond

        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Resona");

        var httpClient = new HttpClient();

        AudioEngine = new AudioEngineService();
        Normalization = new NormalizationService();
        Lyrics = new LyricsService(httpClient);
        CoverArt = new CoverArtService(httpClient, Path.Combine(appData, "Covers"));
        PlaylistIO = new PlaylistM3uService();
        Cache = new LibraryCacheService(appData);
        Scanner = new LibraryScannerService();
        Settings = new SettingsService(appData);

        // IMPORTANT : ne JAMAIS attendre une Task ici, avant la création/activation
        // de la fenêtre. À ce stade du démarrage, le thread UI qui exécute ce code
        // est aussi celui sur lequel les continuations async essaient de revenir :
        // bloquer dessus (même via GetAwaiter().GetResult()) provoque un deadlock
        // certain (le thread s'attend lui-même indéfiniment, à 0% CPU).
        // On utilise donc une version 100% synchrone, sans aucune Task impliquée.
        Settings.LoadSync();
        ApplyThemeResources();

        _window = new MainWindow();
        MainWindowInstance = (MainWindow)_window;

        // ——— Window close → minimize to tray ——————————————————————————————
        _window.AppWindow.Closing += (s, e) =>
        {
            if (Settings.Current.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                if (TrayIcon == null) SetupTrayIcon();
                TrayIcon?.Show();
                _window.AppWindow.Hide();
            }
        };

        _window.Activate();

        _ = Cache.InitializeAsync();

        // 🌟 Démarrage minimisé 🌟
        bool isAutostart = Environment.GetCommandLineArgs().Contains("--autostart");
        if (isAutostart && Settings.Current.StartMinimized && Settings.Current.MinimizeToTrayOnClose)
        {
            if (TrayIcon == null) SetupTrayIcon();
            TrayIcon?.Show();
            _window.AppWindow.Hide();
        }

        // 🌟 Démarrage avec Windows 🌟
        ApplyStartWithWindowsSetting();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, IntPtr uIDNewItem, string lpNewItem);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr hMenu);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public struct POINT { public int X; public int Y; }

    public static void SetupTrayIcon()
    {
        if (TrayIcon != null) return;
        var window = MainWindowInstance;
        if (window == null) return;
        TrayIcon = new TrayIconService(window, "Resona");
        TrayIcon.LeftClick += (s, e) =>
        {
            window.DispatcherQueue.TryEnqueue(() =>
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                ShowWindow(hwnd, 9); // SW_RESTORE
                SetForegroundWindow(hwnd);
                window.AppWindow.Show(true);
                window.AppWindow.MoveInZOrderAtTop();
                TrayIcon?.Hide();
            });
        };
        TrayIcon.RightClick += (s, e) =>
        {
            window.DispatcherQueue.TryEnqueue(() =>
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                SetForegroundWindow(hwnd); // Important pour fermer le menu quand on clique ailleurs

                IntPtr hMenu = CreatePopupMenu();
                InsertMenu(hMenu, 0, 0x0000, (IntPtr)2, "Quitter");
                InsertMenu(hMenu, 1, 0x0000, (IntPtr)1, "Ouvrir Resona");

                GetCursorPos(out POINT pt);
                
                // Affiche le menu et attend le choix (0x0100 = TPM_RETURNCMD)
                uint cmd = TrackPopupMenuEx(hMenu, 0x0100 | 0x0002, pt.X, pt.Y, hwnd, IntPtr.Zero);
                DestroyMenu(hMenu);

                if (cmd == 1)
                {
                    ShowWindow(hwnd, 9);
                    SetForegroundWindow(hwnd);
                    window.AppWindow.Show(true);
                    window.AppWindow.MoveInZOrderAtTop();
                    TrayIcon?.Hide();
                }
                else if (cmd == 2)
                {
                    TrayIcon?.Dispose();
                    TrayIcon = null;
                    AudioEngine.Dispose();
                    Application.Current.Exit();
                }
            });
        };
    }

    public static void ApplyStartWithWindowsSetting()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (Settings.Current.StartWithWindows)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("Resona", $"\"{exePath}\" --autostart");
            }
            else
            {
                if (key.GetValue("Resona") != null)
                    key.DeleteValue("Resona");
            }
        }
        catch { /* Accès registre refusé → ignorer silencieusement */ }
    }

    /// <summary>
    /// Applique les couleurs du thème en MUTANT les objets Brush/Color existants
    /// (plutôt que de remplacer les entrées du dictionnaire de ressources). Comme
    /// tous les contrôles référencent ces MÊMES objets via StaticResource, modifier
    /// leurs propriétés se répercute instantanément partout dans l'UI, sans avoir
    /// besoin de recréer ou recharger la fenêtre.
    /// </summary>
    public static void ApplyThemeResources()
    {
        var res = Application.Current.Resources;
        var settings = Settings.Current;

        bool isSolid = settings.Backdrop == Models.AppBackdropStyle.Solid;
        var preset = Models.ThemePresets.All[Math.Clamp(settings.ThemePresetIndex, 0, Models.ThemePresets.All.Length - 1)];

        // En Mica/Acrylic, on garde un accent neutre pour ne pas teinter le flou
        // système de violet ; en mode Couleur unie, on applique le preset choisi.
        var accent = isSolid ? ParseColor(preset.AccentHex) : Windows.UI.Color.FromArgb(255, 0x66, 0x66, 0x6E);
        var accentSecondary = isSolid ? ParseColor(preset.AccentSecondaryHex) : Windows.UI.Color.FromArgb(255, 0x88, 0x88, 0x90);
        var background = ParseColor(preset.BackgroundHex);
        var surface = isSolid
            ? ParseColor(preset.SurfaceHex)
            // Opaque (alpha=255) et pas semi-transparente : la fusion alpha d'un
            // calque translucide par-dessus le flou Mica/Acrylic a un coût de calcul
            // notable la toute première fois que Windows doit le composer, ce qui
            // causait le flash observé uniquement sur la page Paramètres (seule page
            // à utiliser des "cartes" superposées). Une couleur opaque élimine
            // complètement cette étape de fusion.
            : Windows.UI.Color.FromArgb(255, 0x20, 0x20, 0x24);

        // Détection thème clair (fond lumineux) pour adapter les couleurs de texte.
        // On élargit aussi la détection au SURFACE (le player/cartes) car même si le
        // fond est un peu gris, un surface clair => texte clair illisible.
        bool isLightTheme = isSolid && (background.R > 180 && background.G > 180 && background.B > 180);
        bool isLightSurface = isSolid && (surface.R > 180 && surface.G > 180 && surface.B > 180);
        bool isLight = isLightTheme || isLightSurface;

        // Détection spécifique pour les thèmes de couleur unie extrêmes
        bool isAbsoluteBlack = isSolid && (background.R + background.G + background.B < 24);
        bool isPureWhite = isSolid && (background.R >= 200 && background.G >= 200 && background.B >= 200);

        // Accent fonctionnel : pour le Noir Absolu l'accent du preset est #000000
        // (invisible sur fond noir). On substitue un accent sobre mais visible pour
        // les contrôles qui en dépendent (sliders, badges, bouton play, sélection nav).
        bool isBlackAccent = isSolid && (accent.R + accent.G + accent.B < 48);
        var functionalAccent = isBlackAccent ? Windows.UI.Color.FromArgb(255, 0x6E, 0x78, 0x8C) : accent;
        var functionalAccentLight1 = isBlackAccent ? Windows.UI.Color.FromArgb(255, 0x8A, 0x94, 0xA8) : Lighten(accent, 0.2);

        SetColor(res, "SystemAccentColor", accent);
        SetColor(res, "SystemAccentColorLight1", Lighten(accent, 0.2));
        SetColor(res, "SystemAccentColorLight2", Lighten(accent, 0.4));
        SetColor(res, "SystemAccentColorLight3", Lighten(accent, 0.6));
        SetColor(res, "SystemAccentColorDark1", Darken(accent, 0.85));
        SetColor(res, "SystemAccentColorDark2", Darken(accent, 0.7));
        SetColor(res, "SystemAccentColorDark3", Darken(accent, 0.55));

        SetBrushColor(res, "AccentFillColorDefaultBrush", functionalAccent);
        SetBrushColor(res, "AccentFillColorSecondaryBrush", functionalAccentLight1);
        SetBrushColor(res, "AccentFillColorTertiaryBrush", isBlackAccent ? Windows.UI.Color.FromArgb(255, 0xA8, 0xB2, 0xC6) : Lighten(accent, 0.4));

        // Le brush d'accent "brand" (dégradés, covers vides) garde la couleur du
        // preset : sur le noir absolu on veut quand même un vrai dégradé violet/etc.
        // uniquement les contrôles FONCTIONNELS utilisent functionalAccent ci-dessus.
        SetBrushColor(res, "AppAccentBrush", accent);
        SetBrushColor(res, "AppAccentSecondaryBrush", accentSecondary);
        SetBrushColor(res, "AppDeepBackgroundBrush", background);
        SetBrushColor(res, "AppSurfaceBrush", surface);

        // â”€â”€ Brushes de texte / contrôle : on MUTE les objets existants (jamais de
        //    remplacement res[key]=new Brush). Les contrôles référencent les MÊMES
        //    objets déclarés dans App.xaml → muter leur .Color propage partout. C'est
        //    ce qui rend le thème blanc lisible (sinon le texte reste blanc sur blanc).
        if (isLight)
        {
            SetBrushColor(res, "TextFillColorPrimaryBrush",   Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextFillColorSecondaryBrush", Windows.UI.Color.FromArgb(255, 0x44, 0x44, 0x44));
            SetBrushColor(res, "TextFillColorTertiaryBrush",  Windows.UI.Color.FromArgb(255, 0x72, 0x72, 0x72));
            SetBrushColor(res, "TextFillColorDisabledBrush",  Windows.UI.Color.FromArgb(255, 0xA8, 0xA8, 0xA8));
            SetBrushColor(res, "TextFillColorInverseBrush",   Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ControlFillColorDefaultBrush",     Windows.UI.Color.FromArgb(255, 0xF6, 0xF6, 0xF6));
            SetBrushColor(res, "ControlFillColorSecondaryBrush",   Windows.UI.Color.FromArgb(255, 0xE9, 0xE9, 0xE9));
            SetBrushColor(res, "ControlFillColorTertiaryBrush",    Windows.UI.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));
            SetBrushColor(res, "ControlFillColorTransparentBrush", Windows.UI.Color.FromArgb(0, 0x00, 0x00, 0x00));
            SetBrushColor(res, "ControlStrongStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(255, 0xC4, 0xC4, 0xC4));
            SetBrushColor(res, "CardStrokeColorDefaultBrush",    Windows.UI.Color.FromArgb(255, 0xC8, 0xC8, 0xC8));
            SetBrushColor(res, "DividerStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(255, 0xD0, 0xD0, 0xD0));
            SetBrushColor(res, "ControlAltFillColorTertiaryBrush", Windows.UI.Color.FromArgb(255, 0xE0, 0xE0, 0xE0));
            SetBrushColor(res, "SubtleFillColorTransparentBrush", Windows.UI.Color.FromArgb(0, 0x00, 0x00, 0x00));

            // Brushes spécifiques pour les contrôles
            SetBrushColor(res, "ToggleSwitchForegroundBrush", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "CheckBoxForegroundBrush", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "RadioButtonForegroundBrush", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxForegroundBrush", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxForegroundPressed", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxForegroundDisabled", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxForegroundSelected", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxDropDownGlyphForeground", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxDropDownGlyphForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxDropDownGlyphForegroundPressed", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ComboBoxDropDownGlyphForegroundDisabled", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextBoxForegroundBrush", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextBoxForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextBoxForegroundPressed", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextBoxForegroundDisabled", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextBoxForegroundSelected", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "TextBoxBackground", Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
            SetBrushColor(res, "TextBoxBackgroundPointerOver", Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
            SetBrushColor(res, "TextBoxBackgroundFocused", Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
            SetBrushColor(res, "TextBoxBorderBrush", Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0));
            SetBrushColor(res, "TextBoxBorderBrushPointerOver", Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0));
            SetBrushColor(res, "TextBoxBorderBrushFocused", Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0));
            SetBrushColor(res, "TextControlBackground", Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
            SetBrushColor(res, "TextControlBackgroundPointerOver", Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
            SetBrushColor(res, "TextControlBackgroundFocused", Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
            SetBrushColor(res, "TextControlBorderBrush", Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0));
            SetBrushColor(res, "TextControlBorderBrushPointerOver", Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0));
            SetBrushColor(res, "TextControlBorderBrushFocused", Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0));
            SetBrushColor(res, "ToggleSwitchHeaderForeground", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ToggleSwitchOnForeground", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ToggleSwitchOffForeground", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "CheckBoxCheckGlyphForeground", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "RadioButtonCheckGlyphForeground", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));

            // Fond des items de nav : survol/sélection en gris clair (pas blanc semi-transparent).
            SetBrushColor(res, "NavigationViewItemBackground",            Windows.UI.Color.FromArgb(0, 0x00, 0x00, 0x00));
            SetBrushColor(res, "NavigationViewItemBackgroundPointerOver", Windows.UI.Color.FromArgb(255, 0xE4, 0xE4, 0xE4));
            SetBrushColor(res, "NavigationViewItemBackgroundPressed",     Windows.UI.Color.FromArgb(255, 0xD6, 0xD6, 0xD6));
            SetBrushColor(res, "NavigationViewItemBackgroundSelected",    Windows.UI.Color.FromArgb(255, 0xEC, 0xEC, 0xEC));
            SetBrushColor(res, "NavigationViewItemForeground",            Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "NavigationViewItemForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "NavigationViewItemForegroundPressed",     Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "NavigationViewItemForegroundSelected",    Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "NavigationViewItemForegroundSelectedPointerOver", Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
        }
        else
        {
            SetBrushColor(res, "TextFillColorPrimaryBrush",   Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextFillColorSecondaryBrush", Windows.UI.Color.FromArgb(200, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextFillColorTertiaryBrush",  Windows.UI.Color.FromArgb(160, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextFillColorDisabledBrush",  Windows.UI.Color.FromArgb(92, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextFillColorInverseBrush",   Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            SetBrushColor(res, "ControlFillColorDefaultBrush",     Windows.UI.Color.FromArgb(15, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ControlFillColorSecondaryBrush",   Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ControlFillColorTertiaryBrush",    Windows.UI.Color.FromArgb(8, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ControlFillColorTransparentBrush", Windows.UI.Color.FromArgb(0, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ControlStrongStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "CardStrokeColorDefaultBrush",    Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "DividerStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ControlAltFillColorTertiaryBrush", Windows.UI.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "SubtleFillColorTransparentBrush", Windows.UI.Color.FromArgb(0, 0xFF, 0xFF, 0xFF));

            // Brushes spécifiques pour les contrôles (thèmes sombres)
            SetBrushColor(res, "ToggleSwitchForegroundBrush", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "CheckBoxForegroundBrush", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "RadioButtonForegroundBrush", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxForegroundBrush", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxForegroundPressed", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxForegroundDisabled", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxForegroundSelected", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxDropDownGlyphForeground", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxDropDownGlyphForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxDropDownGlyphForegroundPressed", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ComboBoxDropDownGlyphForegroundDisabled", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxForegroundBrush", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxForegroundPressed", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxForegroundDisabled", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxForegroundSelected", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxBackground", Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxBackgroundPointerOver", Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxBackgroundFocused", Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxBorderBrush", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxBorderBrushPointerOver", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextBoxBorderBrushFocused", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextControlBackground", Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextControlBackgroundPointerOver", Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextControlBackgroundFocused", Windows.UI.Color.FromArgb(24, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextControlBorderBrush", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextControlBorderBrushPointerOver", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "TextControlBorderBrushFocused", Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ToggleSwitchHeaderForeground", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ToggleSwitchOnForeground", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "ToggleSwitchOffForeground", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "CheckBoxCheckGlyphForeground", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "RadioButtonCheckGlyphForeground", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));

            SetBrushColor(res, "NavigationViewItemBackground",            Windows.UI.Color.FromArgb(0, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemBackgroundPointerOver", Windows.UI.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemBackgroundPressed",     Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemBackgroundSelected",    Windows.UI.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemForeground",            Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemForegroundPointerOver", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemForegroundPressed",     Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemForegroundSelected",    Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "NavigationViewItemForegroundSelectedPointerOver", Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
        }

        // AppAccentForegroundBrush : contraste sur le fond accent.
        // Luminance perceptuelle (0.299R + 0.587G + 0.114B) : si l'accent est clair, texte noir, sinon blanc.
        double accentLuma = 0.299 * functionalAccent.R + 0.587 * functionalAccent.G + 0.114 * functionalAccent.B;
        var accentFg = accentLuma > 140
            ? Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A)   // accent clair → texte noir
            : Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF);   // accent sombre → texte blanc
        SetBrushColor(res, "AppAccentForegroundBrush", accentFg);

        // Thumb et piste des sliders : suivent l'accent FONCTIONNEL (visible sur noir)
        SetBrushColor(res, "SliderThumbBackground", functionalAccent);
        SetBrushColor(res, "SliderThumbBackgroundPointerOver", functionalAccentLight1);
        SetBrushColor(res, "SliderThumbBackgroundPressed", isBlackAccent ? Windows.UI.Color.FromArgb(255, 0xA8, 0xB2, 0xC6) : Lighten(accent, 0.4));
        SetBrushColor(res, "SliderTrackValueFill", functionalAccent);
        SetBrushColor(res, "SliderTrackValueFillPointerOver", functionalAccentLight1);
        SetBrushColor(res, "SliderTrackValueFillPressed", isBlackAccent ? Windows.UI.Color.FromArgb(255, 0xA8, 0xB2, 0xC6) : Lighten(accent, 0.4));

        // Adaptations spécifiques pour les thèmes extrêmes (Noir Absolu / Blanc Pur)
        if (isAbsoluteBlack)
        {
            // Pour le noir absolu, s'assurer que tous les éléments UI sont visibles en blanc/gris clair
            // Les contrôles de navigation et sliders utilisent déjà functionalAccent (gris-bleu visible)
            // On s'assure que les bordures et séparateurs sont aussi visibles
            SetBrushColor(res, "ControlStrongStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "CardStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
            SetBrushColor(res, "DividerStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
        }
        else if (isPureWhite)
        {
            // Pour le blanc pur, s'assurer que les bordures et séparateurs sont visibles en gris foncé
            SetBrushColor(res, "ControlStrongStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(255, 0xC4, 0xC4, 0xC4));
            SetBrushColor(res, "CardStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(255, 0xC8, 0xC8, 0xC8));
            SetBrushColor(res, "DividerStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(255, 0xD0, 0xD0, 0xD0));
        }

        if (res["AppAccentGradientBrush"] is Microsoft.UI.Xaml.Media.LinearGradientBrush gradient && gradient.GradientStops.Count >= 2)
        {
            // Pour le noir absolu, le dégradé utiliserait du noir invisible → on prend
            // l'accent fonctionnel (gris-bleu) pour que les covers vides / avatars restent visibles.
            gradient.GradientStops[0].Color = isBlackAccent ? functionalAccent : accent;
            gradient.GradientStops[1].Color = isBlackAccent ? functionalAccentLight1 : accentSecondary;
        }

        // Le conteneur interne de NavigationView a sa propre couleur de fond par
        // défaut (souvent un gris/noir système) qui flashe brièvement pendant la
        // navigation entre pages, par-dessus l'effet Mica/Acrylic. On la force en
        // transparent pour laisser le flou système s'afficher sans interruption.
        res["NavigationViewDefaultContentBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        res["NavigationViewTopContentBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        res["NavigationViewContentBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        res["NavigationViewDefaultPaneBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        res["NavigationViewExpandedPaneBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        res["NavigationViewCompactPaneBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private static void SetColor(ResourceDictionary res, string key, Windows.UI.Color color)
    {
        if (res.ContainsKey(key)) res[key] = color;
    }

    private static void SetBrushColor(ResourceDictionary res, string key, Windows.UI.Color color)
    {
        if (res.ContainsKey(key) && res[key] is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
        {
            try
            {
                brush.Color = color;
            }
            catch
            {
                // Certains brushes sont en lecture seule, on les ignore
            }
        }
        else if (!res.ContainsKey(key))
        {
            // Si la brush n'existe pas, on la crée
            try
            {
                res[key] = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
            }
            catch
            {
                // Ignore les erreurs de création
            }
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    private static Windows.UI.Color Lighten(Windows.UI.Color c, double amount)
        => Windows.UI.Color.FromArgb(255,
            (byte)Math.Min(255, c.R + (255 - c.R) * amount),
            (byte)Math.Min(255, c.G + (255 - c.G) * amount),
            (byte)Math.Min(255, c.B + (255 - c.B) * amount));

    private static Windows.UI.Color Darken(Windows.UI.Color c, double factor)
        => Windows.UI.Color.FromArgb(255, (byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));
}

