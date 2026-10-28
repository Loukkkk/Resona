// ============================================================
//  Models.cs  —  Resona
//  Fusion de : Track.cs, Playlist.cs, AppSettings.cs, ThemePresets.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Resona.Models;

// ------------------------------------------------------------
//  Track
// ------------------------------------------------------------

public class Track : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        {
        if (Resona.App.MainWindowInstance?.DispatcherQueue != null && !Resona.App.MainWindowInstance.DispatcherQueue.HasThreadAccess)
        {
            Resona.App.MainWindowInstance.DispatcherQueue.TryEnqueue(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
        }
        else
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _filePath = string.Empty;
    public string FilePath { get => _filePath; set { if (_filePath != value) { _filePath = value; Raise(); } } }

    private string _title = "Titre inconnu";
    public string Title { get => _title; set { if (_title != value) { _title = value; Raise(); } } }

    private string _artist = "Artiste inconnu";
    public string Artist { get => (_artist == "Artiste inconnu" || _artist == "Unknown artist" || string.IsNullOrWhiteSpace(_artist)) ? Strings.Current.CS_ArtisteInconnu : _artist; set { if (_artist != value) { _artist = value; Raise(); Raise(nameof(DisplayArtist)); } } }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayArtist { get { var a = Artist; return Resona.App.Settings.Current.ArtistMappings.TryGetValue(a, out var m) ? m : a; } }

    private string _album = "Album inconnu";
    public string Album { get => (_album == "Album inconnu" || _album == "Unknown album" || string.IsNullOrWhiteSpace(_album)) ? Strings.Current.CS_AlbumInconnu : _album; set { if (_album != value) { _album = value; Raise(); Raise(nameof(DisplayAlbum)); } } }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayAlbum { get { var a = Album; return Resona.App.Settings.Current.AlbumMappings.TryGetValue(a, out var m) ? m : a; } }

    private string _albumArtist = string.Empty;
    public string AlbumArtist { get => _albumArtist; set { if (_albumArtist != value) { _albumArtist = value; Raise(); } } }

    private TimeSpan _duration;
    public TimeSpan Duration 
    { 
        get => _duration; 
        set 
        { 
            if (_duration != value) 
            { 
                _duration = value; 
                Raise(); 
                Raise(nameof(DurationDisplay)); 
            } 
        } 
    }

    public string DurationDisplay => Duration.TotalSeconds > 0
        ? (Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"m\:ss"))
        : "--:--";

    private int _trackNumber;
    public int TrackNumber { get => _trackNumber; set { if (_trackNumber != value) { _trackNumber = value; Raise(); } } }

    private int _year;
    public int Year { get => _year; set { if (_year != value) { _year = value; Raise(); } } }

    private string _genre = string.Empty;
    public string Genre { get => _genre; set { if (_genre != value) { _genre = value; Raise(); Raise(nameof(DisplayGenre)); } } }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayGenre
    {
        get
        {
            var settings = Resona.App.Settings.Current;
            // Correspondance propre à ce titre (nettoyage IA des genres) en priorité, puis correspondance par ancien genre.
            if (settings.TrackGenreMappings.Count > 0 && settings.TrackGenreMappings.TryGetValue(GenreMappingKey, out var perTrack)) return perTrack;
            var a = Genre;
            return settings.GenreMappings.TryGetValue(a, out var m) ? m : a;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string GenreMappingKey => $"{Artist} - {Title}".Trim();

    public string YearAndGenreDisplay 
    {
        get 
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Year > 0) parts.Add(Year.ToString());
            if (!string.IsNullOrWhiteSpace(DisplayGenre) && DisplayGenre != "Unknown genre" && DisplayGenre != "Genre inconnu" && DisplayGenre != "Unknown" && DisplayGenre != "Inconnu") parts.Add(DisplayGenre);
            return parts.Count > 0 ? " • " + string.Join(" • ", parts) : "";
        }
    }

    private string? _coverArtPath;
    public string? CoverArtPath
    {
        get => _coverArtPath;
        set { if (_coverArtPath != value) { _coverArtPath = value; Raise(); } }
    }

    private string? _lyrics;
    public string? Lyrics
    {
        get => _lyrics;
        set { if (_lyrics != value) { _lyrics = value; Raise(); } }
    }

    public bool LyricsSynced { get; set; }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                Raise();
                Raise(nameof(OpacityPlaying));
                Raise(nameof(OpacityPlayingBg));
            }
        }
    }

    public double OpacityPlaying => IsPlaying ? 1.0 : 0.0;
    public double OpacityPlayingBg => IsPlaying ? 0.25 : 0.0;

    // Etat favori transitoire (source de verite = appartenance a la playlist systeme
    // Favoris), rafraichi a l'affichage des listes pour piloter le coeur au survol.
    [System.Text.Json.Serialization.JsonIgnore]
    private bool _isFavorite;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                Raise();
                Raise(nameof(FavoriteGlyph));
            }
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string FavoriteGlyph => IsFavorite ? "\ue00b" : "\ue006";

    private double _normalizationGainDb;
    public double NormalizationGainDb
    {
        get => _normalizationGainDb;
        set { if (_normalizationGainDb != value) { _normalizationGainDb = value; Raise(); } }
    }

    public bool     IsAnalyzed   { get; set; }

    // Son ouvert depuis Windows (double-clic, "Ouvrir avec Resona") : lu uniquement, jamais ajoute a la bibliotheque
    // (jamais enregistre dans le cache de la bibliotheque, qui ignore ces sons).
    [System.Text.Json.Serialization.JsonIgnore]
    public bool     IsExternal   { get; set; }
    public DateTime DateAdded    { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; }

    public override string ToString() => $"{Artist} - {Title}";
}

// ------------------------------------------------------------
//  Playlist
// ------------------------------------------------------------

public class Playlist
{
    public string       Id           { get; set; } = Guid.NewGuid().ToString("N");
    public string       Name         { get; set; } = "Nouvelle playlist";
    public string       CoverImagePath { get; set; } = string.Empty;
    public List<string> TrackIds     { get; set; } = new();
    public DateTime     DateCreated  { get; set; } = DateTime.UtcNow;
    public DateTime     DateModified { get; set; } = DateTime.UtcNow;
    // Playlist systeme (ex. Favoris) : non renommable, non supprimable, pochette non modifiable,
    // toujours affichee en tete de liste.
    public bool          IsSystem     { get; set; } = false;
    // Playlist intelligente : definition des regles en JSON (voir SmartPlaylist.cs). null / vide = playlist classique.
    // Pour une playlist intelligente, TrackIds reste vide : le contenu est recalcule a chaque ouverture.
    public string?       SmartRulesJson { get; set; } = null;

    public const string FavoritesId = "__favorites__";
}

// ------------------------------------------------------------
//  AppSettings
// ------------------------------------------------------------

// Halloween est AJOUTE EN DERNIER : les valeurs existantes (Mica=0, MicaAlt=1, Acrylic=2, Solid=3) restent
// inchangees, donc les settings.json existants restent valides (aucune migration necessaire).
// Christmas est AJOUTE apres Halloween pour la meme raison (valeurs existantes inchangees).
public enum AppBackdropStyle { Mica, MicaAlt, Acrylic, Solid, Halloween, Christmas }

public static class AppBackdropStyleExtensions
{
    /// <summary>Vrai pour les fonds "couleur unie" (Solid, Halloween, Christmas) : pas de materiau systeme, couleurs issues d'un preset.</summary>
    public static bool IsSolidLike(this AppBackdropStyle style)
        => style == AppBackdropStyle.Solid || style == AppBackdropStyle.Halloween || style == AppBackdropStyle.Christmas;

    /// <summary>Vrai pour les themes saisonniers (disponibles uniquement pendant leur periode).</summary>
    public static bool IsSeasonal(this AppBackdropStyle style)
        => style == AppBackdropStyle.Halloween || style == AppBackdropStyle.Christmas;
}

/// <summary>
/// Disponibilite des themes saisonniers : Halloween = octobre, Noel = decembre.
/// Hors periode, l'option n'est plus proposee dans les parametres et un theme saisonnier deja choisi repasse en "Couleur unie".
/// Pour tester hors saison : build Debug (tout est disponible) ou variable d'environnement
/// RESONA_FORCE_SEASON = halloween | christmas | all.
/// </summary>
public static class SeasonalThemes
{
    public static bool IsAvailable(AppBackdropStyle style, DateTime? now = null)
    {
        if (!style.IsSeasonal()) return true;
#if DEBUG
        return true;
#else
        string? force = Environment.GetEnvironmentVariable("RESONA_FORCE_SEASON")?.Trim().ToLowerInvariant();
        if (force == "all") return true;
        if (force == "halloween" && style == AppBackdropStyle.Halloween) return true;
        if (force == "christmas" && style == AppBackdropStyle.Christmas) return true;

        int month = (now ?? DateTime.Now).Month;
        return style switch
        {
            AppBackdropStyle.Halloween => month == 10,
            AppBackdropStyle.Christmas => month == 12,
            _ => true,
        };
#endif
    }

    /// <summary>Si le theme saisonnier choisi est hors periode, repasse en Couleur unie. Renvoie vrai si le reglage a change.</summary>
    public static bool EnforceSeason(AppSettings settings)
    {
        if (IsAvailable(settings.Backdrop)) return false;
        settings.Backdrop = AppBackdropStyle.Solid;
        return true;
    }
}

public enum DownloadFormat
{
    Mp3,
    Flac,
    Opus,
    M4a,
    Wav,
    Vorbis,
}

public enum DownloadBitrate
{
    Best,
    Kbps320,
    Kbps256,
    Kbps192,
    Kbps128,
}

public class AppSettings
{
    public string LastKnownVersion { get; set; } = "";
    public bool AutoUpdateEnabled { get; set; } = false;
    // Derniere version de l'app dont le patch note a ete affiche ("" = jamais). Sert a afficher les nouveautes
    // une seule fois, au premier lancement apres une mise a jour. Absent des anciens settings.json -> valeur par defaut.
    public string LastSeenVersion { get; set; } = "";

    // Mise a jour automatique de yt-dlp (desactivee par defaut) : verification au plus une fois par jour, en arriere-plan.
    // LastYtDlpUpdateCheckUtc = date (ISO 8601, UTC) de la derniere verification reussie.
    public bool   AutoUpdateYtDlp           { get; set; } = false;
    public string LastYtDlpUpdateCheckUtc   { get; set; } = "";

    // Parametres : cles des sections repliees (vide = tout est deplie)
    public List<string> CollapsedSettingsSections { get; set; } = new();

    // Localisation
    public string AppLanguage { get; set; } = "";
    public bool SaveWindowPosition { get; set; } = false;
    public bool SaveWindowSize { get; set; } = false;
    public bool EnableUpNextPanel { get; set; } = true;
    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 800;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;

    // Bibliothèque
    public List<string> MusicFolders           { get; set; } = new();
    // Fichiers retirés de la bibliothèque ("Retirer de l'app") : ignorés lors des scans, sans être supprimés du disque
    public List<string> ExcludedFilePaths      { get; set; } = new();
    public bool         WatchFoldersEnabled    { get; set; } = true;
    public bool         HasCompletedOnboarding { get; set; } = false;

    // Fonctionnalités et audio
    public double Volume               { get; set; } = 100.0;
    public int SavedPlaybackMode       { get; set; } = 0;
    public bool NormalizationEnabled   { get; set; } = false;
    public bool WeightedShuffleEnabled { get; set; } = false;

    // Integration a Windows : entrees "Ouvrir avec Resona" dans le menu de l'Explorateur (dossiers et sons)
    public bool ExplorerContextMenuEnabled { get; set; } = true;
    public double NormalizationMaxGain   { get; set; } = 12.0;
    public bool LyricsEnabled          { get; set; } = true;
    public bool TranslateLyricsEnabled { get; set; } = false;
    public bool AutoFetchMissingCovers { get; set; } = false;
    public bool AutoTagWriteToFile     { get; set; } = false;
    public bool ExclusiveAudioMode     { get; set; } = false;
    // Ignorer les silences au debut et a la fin des sons (desactive par defaut ; absent des anciens settings.json -> false)
    public bool SkipSilenceEnabled     { get; set; } = false;
    // Mode soiree (niveau 1 : fondu intelligent) : active d'office le fondu enchaine et le saut des silences (desactive par defaut ; absent des anciens settings.json -> false)
    public bool PartyModeEnabled       { get; set; } = false;
    // Audio ducking : baisse le volume de Resona quand l'application choisie emet du son (desactive par defaut)
    public bool   DuckingEnabled          { get; set; } = false;
    public string DuckingProcessName      { get; set; } = "";    // nom du processus surveille (sans .exe)
    public double DuckingAmountDb         { get; set; } = 12.0;  // baisse maximale en dB
    public double DuckingThresholdPercent { get; set; } = 2.0;   // niveau crete minimal de l'application pour declencher (en %)
    public int    DuckingAttackMs         { get; set; } = 150;   // duree de la baisse
    public int    DuckingHoldMs           { get; set; } = 600;   // maintien apres le dernier son detecte
    public int    DuckingReleaseMs        { get; set; } = 1000;  // duree de la remontee
    public int    DuckingCurve            { get; set; } = 1;     // pente : 0 lineaire, 1 progressive (S), 2 rapide puis douce
    public bool AutoOpenNowPlaying    { get; set; } = false;

    // IA
    public List<string> SavedQueueIds { get; set; } = new();
    public bool AIEnabled { get; set; } = false;
    public Dictionary<string, string> ArtistMappings { get; set; } = new();
    public Dictionary<string, string> AlbumMappings { get; set; } = new();
    public Dictionary<string, string> GenreMappings { get; set; } = new();
    // Genre attribué par le nettoyage IA, par titre (clé « Artiste - Titre »)
    public Dictionary<string, string> TrackGenreMappings { get; set; } = new();
    
    // Apparence
    public AppBackdropStyle Backdrop                   { get; set; } = AppBackdropStyle.Solid;
    public int              ThemePresetIndex           { get; set; } = 0;
    // Theme de couleur personnalise (selecteur de couleur + fond en degrade). Valeurs par defaut = comportement existant
    // inchange : les anciens settings.json restent valides (proprietes absentes -> valeurs par defaut).
    public bool             UseCustomTheme             { get; set; } = false;
    public string           CustomAccentHex            { get; set; } = "#7C5CFF";
    public bool             CustomGradientEnabled      { get; set; } = false;
    public List<string>     CustomGradientColors       { get; set; } = new();
    // Grain d'arriere-plan (texture de bruit au-dessus du fond, sous l'interface) : valeurs par defaut = desactive.
    public bool             GrainEnabled               { get; set; } = false;
    public int              GrainAmount                { get; set; } = 35;   // 5..100
    public bool             PlayerGradientOverflowEnabled { get; set; } = true;
    // Gradient à l'intérieur du lecteur (si désactivé, le gradient qui dépasse est aussi désactivé)
    public bool             PlayerGradientEnabled         { get; set; } = true;
    // Thème réactif au son (module la couleur du thème au rythme de la musique)
    public bool             AudioReactiveThemeEnabled     { get; set; } = false;
    // Animation equalizer en bas de la grosse cover centrale du lecteur (desactivee par defaut)
    public bool             NowPlayingEqualizerEnabled    { get; set; } = false;

    // Catégories visibles dans la nav
    public bool ShowLibraryCategory    { get; set; } = true;
    public bool ShowAlbumsCategory     { get; set; } = true;
    public bool ShowPlaylistsCategory  { get; set; } = true;
    public bool ShowArtistsCategory    { get; set; } = true;
    public bool ShowStatisticsCategory { get; set; } = true;
    public bool ShowQueueCategory      { get; set; } = true;
    public bool ShowDownloadCategory   { get; set; } = true;
    // Paramètres d'affichage des pages
    public string LibrarySort { get; set; } = "artist_asc";
    public string AlbumsSort { get; set; } = "name_asc";
    public int AlbumsDisplayCountIndex { get; set; } = 0;
    public string ArtistsSort { get; set; } = "name_asc";
    public int ArtistsDisplayCountIndex{ get; set; } = 0;
    public string GenresSort { get; set; } = "name_asc";
    public string FoldersSort { get; set; } = "name_asc";
    public string PlaylistsSort { get; set; } = "name_asc";
    public int GenresDisplayCountIndex { get; set; } = 0;
    public bool ShowGenresCategory     { get; set; } = true;
    public bool ShowFoldersCategory    { get; set; } = true;

    public int LibraryDisplayLimit { get; set; } = 50;

    // Nombre de cartes affichées par page (-1 = tout afficher)
    public int AlbumsDisplayLimit { get; set; } = 50;
    public int ArtistsDisplayLimit { get; set; } = 50;
    public int GenresDisplayLimit { get; set; } = 50;
    public int FoldersDisplayLimit { get; set; } = 50;
    public int PlaylistsDisplayLimit { get; set; } = -1;

    // Téléchargement
    public string DownloadFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    public DownloadFormat DownloadFormat { get; set; } = DownloadFormat.Opus;
    public string DownloadCodec { get; set; } = string.Empty;
    public DownloadBitrate DownloadBitrate { get; set; } = DownloadBitrate.Best;

    // Système
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool EnableMiniPlayerButton { get; set; } = true;
    public bool MiniPlayerAlwaysOnTop { get; set; } = true;
    public int MiniPlayerWidth { get; set; } = 0;   // 0 = taille par defaut (340)
    public int MiniPlayerHeight { get; set; } = 0;  // 0 = taille par defaut (600)
    public bool EnableDiscordRichPresence { get; set; } = false;
    // Derniere annee pour laquelle on a propose le Year in review au lancement (0 = jamais) : une seule proposition par annee.
    public int YearInReviewPromptedYear { get; set; } = 0;
    // [OBSOLETE — conserve pour ne pas casser la deserialisation des anciens fichiers settings.json]
    public bool YearInReviewPromptDisabled { get; set; } = false;
    // Annee pour laquelle l'utilisateur a choisi "Ne plus me le proposer" (0 = jamais). Se reinitialise automatiquement l'annee suivante.
    public int YearInReviewPromptDismissedYear { get; set; } = 0;
    // Ordre d'affichage des categories dans le volet de gauche (cles : library, albums, playlists, artists, genres, folders, statistics, download).
    // Vide = ordre par defaut. La file d'attente suit toujours la categorie Playlists.
    public List<string> NavCategoryOrder { get; set; } = new();

    // Boutons de la PlayerBar (favoris / egaliseur rapide)
    public bool EnableFavoriteButton { get; set; } = true;
    public bool EnableEqualizerQuickButton { get; set; } = true;

    // Traitement Audio (Crossfade / Egaliseur)
    public bool EnableCrossfade { get; set; } = false;
    public int CrossfadeDurationSeconds { get; set; } = 3;
    public bool EqualizerEnabled { get; set; } = false;
    public double[] EqualizerBands { get; set; } = new double[10] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
}

// ------------------------------------------------------------
//  ThemePresets
// ------------------------------------------------------------

public record ThemePreset(
    string Name,
    string AccentHex,
    string AccentSecondaryHex,
    string BackgroundHex,
    string SurfaceHex);

public static class ThemePresets
{
    // Preset du fond Halloween : volontairement HORS de All, pour ne pas apparaitre comme pastille de couleur
    // dans la palette du mode Couleur unie.
    public static readonly ThemePreset Halloween = new("Halloween", "#FF7A1A", "#9B4DFF", "#120A1C", "#241437");

    // Preset du fond Noel : meme principe (hors de All). Rouge de Noel + vert sapin sur un fond bleu nuit enneige.
    public static readonly ThemePreset Christmas = new("Christmas", "#E63946", "#2FBF71", "#0A1A2B", "#12304A");

    /// <summary>Preset effectivement applique : celui d'Halloween si ce fond est actif, sinon la pastille choisie.</summary>
    public static ThemePreset Resolve(AppSettings settings)
    {
        if (settings.Backdrop == AppBackdropStyle.Halloween) return Halloween;
        if (settings.Backdrop == AppBackdropStyle.Christmas) return Christmas;
        if (settings.Backdrop == AppBackdropStyle.Solid && settings.UseCustomTheme) return CustomTheme.Build(settings);
        return All[Math.Clamp(settings.ThemePresetIndex, 0, All.Length - 1)];
    }

    public static readonly ThemePreset[] All =
    {
        new("Violet Nuit",  "#7C5CFF", "#FF5CA8", "#161129", "#241E42"),
        new("Bleu Océan",   "#4FA3FF", "#5CE1FF", "#102033", "#1A304D"),
        new("Émeraude",     "#2ED6A1", "#7CFFCB", "#0D2920", "#174233"),
        new("Corail",       "#FF6B5C", "#FFA35C", "#291611", "#42251D"),
        new("Rose Magenta", "#FF5CA8", "#FF8FD0", "#291123", "#421D36"),
        new("Ambre",        "#FFA63D", "#FFD15C", "#291E11", "#42321D"),
        new("Gris Ardoise", "#8A9BB0", "#B0BEC5", "#1C1F29", "#2D3242"),
        new("Noir Absolu",  "#000000", "#222222", "#000000", "#0D0D0D"),
        new("Blanc Pur",    "#FFFFFF", "#CCCCCC", "#F0F0F0", "#E0E0E0"),
    };
}

// ------------------------------------------------------------
//  CustomTheme : theme de couleur choisi avec un selecteur (+ degrade optionnel)
// ------------------------------------------------------------

public static class CustomTheme
{
    public const int MinGradientColors = 2;
    public const int MaxGradientColors = 5;
    private const string DefaultAccent = "#7C5CFF";
    // Les fonds en degrade sont assombris au-dela de cette valeur (HSV, 0..1) pour que le texte clair reste lisible.
    // HSV (comme le selecteur de couleur de Windows) : la teinte et la saturation choisies sont conservees, seule la clarte baisse.
    private const double MaxBackgroundValue = 0.5;

    public static bool IsValidHex(string? hex)
        => !string.IsNullOrWhiteSpace(hex) && hex.TrimStart('#').Length == 6
           && int.TryParse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static string Sanitize(string? hex, string fallback = DefaultAccent) => IsValidHex(hex) ? "#" + hex!.TrimStart('#').ToUpperInvariant() : fallback;

    /// <summary>Couleurs du degrade de fond, deja assombries si besoin. Liste vide si le degrade n'est pas actif.</summary>
    public static List<string> EffectiveGradient(AppSettings s)
    {
        var result = new List<string>();
        if (s.Backdrop != AppBackdropStyle.Solid || !s.UseCustomTheme || !s.CustomGradientEnabled) return result;
        foreach (string hex in s.CustomGradientColors)
        {
            if (!IsValidHex(hex)) continue;
            var (h, sat, v) = ToHsv(hex);
            result.Add(FromHsv(h, sat, Math.Min(v, MaxBackgroundValue)));
            if (result.Count == MaxGradientColors) break;
        }
        return result.Count >= MinGradientColors ? result : new List<string>();
    }

    public static ThemePreset Build(AppSettings s)
    {
        string accent = Sanitize(s.CustomAccentHex);
        var (h, sat, l) = ToHsl(accent);
        string secondary = FromHsl((h + 40) % 360, sat, l);

        string background, surface;
        var gradient = EffectiveGradient(s);
        if (gradient.Count >= MinGradientColors)
        {
            // Moyenne des couleurs du degrade : sert a la detection clair/sombre et aux surfaces (lecteur, cartes).
            double r = 0, g = 0, b = 0;
            foreach (string c in gradient) { var (cr, cg, cb) = ToRgb(c); r += cr; g += cg; b += cb; }
            int n = gradient.Count;
            string avg = FromRgb((int)(r / n), (int)(g / n), (int)(b / n));
            var (ah, asat, al) = ToHsl(avg);
            background = avg;
            surface = FromHsl(ah, asat, Math.Min(0.5, al + 0.07));
        }
        else
        {
            // Fond uni : il SUIT la couleur choisie, comme chaque couleur d'un degrade (meme teinte, meme saturation), au lieu
            // d'un fond quasi noir avec une simple teinte. Plus la couleur est pale (on descend dans le selecteur), plus le fond
            // s'eclaircit, de facon continue. Il reste plafonne (MaxBackgroundValue) pour que le texte blanc reste lisible.
            var (hv, sv, vv) = ToHsv(accent);
            double bgV = Math.Min(MaxBackgroundValue, (0.16 + 0.34 * (1.0 - sv)) * (0.35 + 0.65 * vv));
            background = FromHsv(hv, Math.Min(0.85, sv), bgV);
            surface = FromHsv(hv, Math.Min(0.80, sv * 0.9), Math.Min(MaxBackgroundValue + 0.04, bgV + 0.07));
        }
        return new ThemePreset("Custom", accent, secondary, background, surface);
    }

    // Saturation minimale (HSV, 0..1) autorisee dans le selecteur : en dessous, la couleur est trop proche du blanc/gris,
    // le theme ne peut plus la montrer (fond neutre, accent illisible). Le curseur du selecteur s'arrete a cette limite.
    public const double MinPickerSaturation = 0.30;

    public static double HueOf(string? hex) => ToHsv(Sanitize(hex)).H;

    /// <summary>
    /// Rend une couleur choisie trop blanche (saturation sous le minimum) a la limite autorisee, en gardant sa teinte et sa clarte.
    /// Quand la couleur est blanche/grise pure, sa teinte est indefinie : on garde la derniere teinte connue (lastHue).
    /// </summary>
    public static (string Hex, double Hue, bool Changed) LimitWhiteness(string hex, double lastHue)
    {
        var (h, s, v) = ToHsv(Sanitize(hex));
        double hue = s < 0.02 ? lastHue : h;
        // Petite tolerance : l'aller-retour RGB entier <-> HSV ne doit pas relancer la correction en boucle.
        if (s < MinPickerSaturation - 0.015)
            return (FromHsv(hue, MinPickerSaturation, v), hue, true);
        return (Sanitize(hex), hue, false);
    }

    /// <summary>Decale la teinte d'une couleur (utilise pour proposer une 2e couleur de degrade).</summary>
    public static string ShiftHue(string hex, double degrees)
    {
        var (h, sat, l) = ToHsl(Sanitize(hex));
        return FromHsl(((h + degrees) % 360 + 360) % 360, sat, l);
    }

    public static (int R, int G, int B) ToRgb(string hex)
    {
        hex = hex.TrimStart('#');
        return (Convert.ToInt32(hex.Substring(0, 2), 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16));
    }

    public static string FromRgb(int r, int g, int b)
        => $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";

    public static (double H, double S, double V) ToHsv(string hex)
    {
        var (ri, gi, bi) = ToRgb(hex);
        double r = ri / 255.0, g = gi / 255.0, b = bi / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = 0;
        if (d > 1e-9)
        {
            if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) * 60.0;
            else if (max == g) h = ((b - r) / d + 2) * 60.0;
            else h = ((r - g) / d + 4) * 60.0;
        }
        return (h, max <= 1e-9 ? 0 : d / max, max);
    }

    public static string FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        double r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return FromRgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    public static (double H, double S, double L) ToHsl(string hex)
    {
        var (ri, gi, bi) = ToRgb(hex);
        double r = ri / 255.0, g = gi / 255.0, b = bi / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0, h = 0, s = 0, d = max - min;
        if (d > 1e-9)
        {
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) * 60.0;
            else if (max == g) h = ((b - r) / d + 2) * 60.0;
            else h = ((r - g) / d + 4) * 60.0;
        }
        return (h, s, l);
    }

    public static string FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1); l = Math.Clamp(l, 0, 1);
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = l - c / 2;
        double r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return FromRgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }
}

// ----------------------------------------------------------------
//  EqualizerPresets
//  Liste partagee des 18 presets d'egaliseur (10 bandes chacun),
//  utilisee par SettingsPage (edition complete) et par le flyout
//  rapide de la PlayerBar (acces rapide depuis le lecteur), pour
//  eviter toute duplication entre les deux.
// ----------------------------------------------------------------

public record EqualizerPreset(string Name, double[] Bands);

public static class EqualizerPresets
{
    /// <summary>
    /// Retourne le preset dont les 10 bandes correspondent aux bandes donnees (a 0.05 dB pres),
    /// ou null si la configuration est personnalisee.
    /// </summary>
    public static EqualizerPreset? FindMatch(double[]? bands)
    {
        if (bands == null || bands.Length != 10) return null;
        foreach (var preset in All)
        {
            bool same = true;
            for (int i = 0; i < 10; i++)
            {
                if (Math.Abs(preset.Bands[i] - bands[i]) > 0.05) { same = false; break; }
            }
            if (same) return preset;
        }
        return null;
    }

    public static readonly EqualizerPreset[] All =
    {
        new("Flat",         new double[10] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        new("Bass Boost",   new double[10] { 6.0, 5.0, 4.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }),
        new("Rock",         new double[10] { 5.0, 4.0, 3.0, -1.0, -2.0, -1.0, 2.0, 3.0, 4.0, 4.0 }),
        new("Pop",          new double[10] { -1.0, -1.0, 0.0, 2.0, 4.0, 4.0, 2.0, 0.0, -1.0, -2.0 }),
        new("Electro",      new double[10] { 5.0, 4.0, 1.0, 0.0, -2.0, 0.0, 1.0, 3.0, 4.0, 5.0 }),
        new("Classical",    new double[10] { 3.0, 2.0, 1.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 }),
        new("Acoustic",     new double[10] { 2.0, 1.0, 0.0, 1.0, 2.0, 2.0, 3.0, 2.0, 1.0, 0.0 }),
        new("Dance",        new double[10] { 4.0, 3.0, 2.0, 0.0, -1.0, 0.0, 2.0, 3.0, 4.0, 4.0 }),
        new("Hip-Hop",      new double[10] { 5.0, 4.0, 1.0, 0.0, -1.0, -1.0, 1.0, 2.0, 3.0, 4.0 }),
        new("Jazz",         new double[10] { 3.0, 2.0, 0.0, 1.0, 2.0, 2.0, 1.0, 0.0, 1.0, 2.0 }),
        new("Vocal",        new double[10] { -2.0, -1.0, 0.0, 1.0, 3.0, 4.0, 3.0, 1.0, 0.0, -1.0 }),
        new("Treble",       new double[10] { -1.0, -1.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 5.0 }),
        new("Metal",        new double[10] { 4.0, 3.0, 0.0, -2.0, -3.0, -2.0, 0.0, 2.0, 4.0, 5.0 }),
        new("Party",        new double[10] { 5.0, 5.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 5.0, 5.0 }),
        new("R&B",          new double[10] { 3.0, 5.0, 4.0, 1.0, -1.0, -1.0, 1.0, 2.0, 3.0, 4.0 }),
        new("Spoken Word",  new double[10] { -4.0, -2.0, 0.0, 2.0, 4.0, 5.0, 4.0, 2.0, 0.0, -2.0 }),
        new("Piano",        new double[10] { 2.0, 1.0, 0.0, 1.0, 2.0, 3.0, 4.0, 3.0, 2.0, 1.0 }),
        new("Lofi",         new double[10] { 4.0, 5.0, 3.0, 0.0, -2.0, -4.0, -5.0, -5.0, -4.0, -3.0 }),
    };
}

