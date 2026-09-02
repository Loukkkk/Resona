// ============================================================
//  Models.cs  â€”  Resona
//  Fusion de : Track.cs, Playlist.cs, AppSettings.cs, ThemePresets.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Resona.Models;

// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
//  Track
// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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
    public string DisplayGenre { get { var a = Genre; return Resona.App.Settings.Current.GenreMappings.TryGetValue(a, out var m) ? m : a; } }

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

    private double _normalizationGainDb;
    public double NormalizationGainDb
    {
        get => _normalizationGainDb;
        set { if (_normalizationGainDb != value) { _normalizationGainDb = value; Raise(); } }
    }

    public bool     IsAnalyzed   { get; set; }
    public DateTime DateAdded    { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; }

    public override string ToString() => $"{Artist} - {Title}";
}

// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
//  Playlist
// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

public class Playlist
{
    public string       Id           { get; set; } = Guid.NewGuid().ToString("N");
    public string       Name         { get; set; } = "Nouvelle playlist";
    public string       CoverImagePath { get; set; } = string.Empty;
    public List<string> TrackIds     { get; set; } = new();
    public DateTime     DateCreated  { get; set; } = DateTime.UtcNow;
    public DateTime     DateModified { get; set; } = DateTime.UtcNow;
}

// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
//  AppSettings
// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

public enum AppBackdropStyle { Mica, MicaAlt, Acrylic, Solid }

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

    // Localisation
    public string AppLanguage { get; set; } = "";

    // BibliothÃ¨que
    public List<string> MusicFolders           { get; set; } = new();
    public bool         HasCompletedOnboarding { get; set; } = false;

    // FonctionnalitÃ©s et audio
    public double Volume               { get; set; } = 100.0;
    public int SavedPlaybackMode       { get; set; } = 0;
    public bool NormalizationEnabled   { get; set; } = false;
    public double NormalizationTargetRms { get; set; } = -18.0;
    public double NormalizationMaxGain   { get; set; } = 12.0;
    public bool LyricsEnabled          { get; set; } = true;
    public bool TranslateLyricsEnabled { get; set; } = false;
    public bool AutoFetchMissingCovers { get; set; } = false;
    public bool AutoTagWriteToFile     { get; set; } = false;
    public bool ExclusiveAudioMode     { get; set; } = false;
    public bool AutoOpenNowPlaying    { get; set; } = false;

    // IA
    public List<string> SavedQueueIds { get; set; } = new();
    public bool AIEnabled { get; set; } = false;
    public Dictionary<string, string> ArtistMappings { get; set; } = new();
    public Dictionary<string, string> AlbumMappings { get; set; } = new();
    public Dictionary<string, string> GenreMappings { get; set; } = new();
    
    // Apparence
    public AppBackdropStyle Backdrop                   { get; set; } = AppBackdropStyle.Solid;
    public int              ThemePresetIndex           { get; set; } = 0;
    public bool             PlayerGradientOverflowEnabled { get; set; } = true;

    // CatÃ©gories visibles dans la nav
    public bool ShowLibraryCategory    { get; set; } = true;
    public bool ShowAlbumsCategory     { get; set; } = true;
    public bool ShowPlaylistsCategory  { get; set; } = true;
    public bool ShowArtistsCategory    { get; set; } = true;
    public bool ShowStatisticsCategory { get; set; } = true;
    public bool ShowDownloadCategory   { get; set; } = true;
    // ParamÃ¨tres d'affichage des pages
    public string LibrarySort { get; set; } = "artist_asc";
    public int AlbumsSortIndex         { get; set; } = 0;
    public int AlbumsDisplayCountIndex { get; set; } = 0;
    public int ArtistsSortIndex        { get; set; } = 0;
    public int ArtistsDisplayCountIndex{ get; set; } = 0;
    public int GenresSortIndex         { get; set; } = 0;
    public int GenresDisplayCountIndex { get; set; } = 0;
    public bool ShowGenresCategory     { get; set; } = true;
    public bool ShowFoldersCategory    { get; set; } = true;

    public int LibraryDisplayLimit { get; set; } = 50;

    // TÃ©lÃ©chargement
    public string DownloadFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    public DownloadFormat DownloadFormat { get; set; } = DownloadFormat.Opus;
    public string DownloadCodec { get; set; } = string.Empty;
    public DownloadBitrate DownloadBitrate { get; set; } = DownloadBitrate.Best;

    // SystÃ¨me
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;

    // Ã‰galiseur
    public bool EqualizerEnabled { get; set; } = false;
    public double[] EqualizerBands { get; set; } = new double[10] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
}

// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
//  ThemePresets
// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

public record ThemePreset(
    string Name,
    string AccentHex,
    string AccentSecondaryHex,
    string BackgroundHex,
    string SurfaceHex);

public static class ThemePresets
{
    public static readonly ThemePreset[] All =
    {
        new("Violet Nuit",  "#7C5CFF", "#FF5CA8", "#161129", "#241E42"),
        new("Bleu OcÃ©an",   "#4FA3FF", "#5CE1FF", "#102033", "#1A304D"),
        new("Ã‰meraude",     "#2ED6A1", "#7CFFCB", "#0D2920", "#174233"),
        new("Corail",       "#FF6B5C", "#FFA35C", "#291611", "#42251D"),
        new("Rose Magenta", "#FF5CA8", "#FF8FD0", "#291123", "#421D36"),
        new("Ambre",        "#FFA63D", "#FFD15C", "#291E11", "#42321D"),
        new("Gris Ardoise", "#8A9BB0", "#B0BEC5", "#1C1F29", "#2D3242"),
        new("Noir Absolu",  "#000000", "#222222", "#000000", "#0D0D0D"),
        new("Blanc Pur",    "#FFFFFF", "#CCCCCC", "#F0F0F0", "#E0E0E0"),
    };
}





