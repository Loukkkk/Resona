using System.ComponentModel;

using System.Runtime.CompilerServices;
using Resona;



namespace Resona.Models;



public class Strings : INotifyPropertyChanged

{

    public bool IsFr
    {
        get
        {
            var lang = App.Settings.Current.AppLanguage;
            if (string.IsNullOrEmpty(lang))
            {
                lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }
            return lang == "fr";
        }
    }

    private static Strings? _current;

    public static Strings Current => _current ??= new Strings();



    public event PropertyChangedEventHandler? PropertyChanged;

    public string Dialog_StartWithWindowsTitle => IsFr ? "Démarrage avec Windows" : "Start with Windows";
    public string Dialog_StartWithWindowsContent => IsFr 
        ? "Le démarrage automatique de l'application vient d'être activé.\n\nAttention : Si vous déplacez l'application dans un autre dossier, n'oubliez pas de la relancer manuellement une fois depuis son nouvel emplacement avant d'éteindre votre PC. L'application mettra ainsi à jour son emplacement automatiquement pour que cette fonctionnalité continue de marcher !" 
        : "Automatic app startup has just been enabled.\n\nNote: If you move the application to another folder, do not forget to launch it manually once from its new location before turning off your PC. The app will automatically update its location so this feature keeps working!";

    public void NotifyLanguageChanged()
    {
        // Null property name triggers an update for all bindings to this object
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFr"));
    }



    public string CS_AIPromptDJ => IsFr ? "Tu es un DJ IA. Créer une playlist à partir de la Bibliothèque musicale locale fournie, selon la demande : '{0}'.\n\nRenvoie un objet JSON brut avec : 'playlist_name' (Le nom de la playlist) et 'track_ids' (Un tableau de TEXTE/STRINGS contenant UNIQUEMENT les 'id' des chansons sélectionnées. N'oublie pas les guillemets autour de chaque ID)." : "You are an AI DJ. Create a playlist from the provided local music library, based on the request: '{0}'.\n\nReturn a raw JSON object with: 'playlist_name' (The name of the playlist) and 'track_ids' (An array of TEXT/STRINGS containing ONLY the 'id' of the selected songs. Do not forget the quotes around each ID).";

    public string CS_AIPromptCleanup_Albums => IsFr ? "- Pour les albums: Supprimer les 'OST', 'Single', les noms de fichiers qui n'ont pas de sens, regrouper les mêmes albums sous leur vrai nom officiel.\n- Tu DOIS vérifier en ligne (recherche web) pour trouver les vrais noms d'albums et regrouper ceux qui sont similaires sous le même nom exact.\n" : "- For albums: Remove 'OST', 'Single', meaningless filenames, group identical albums under their true official name.\n- You MUST check online (web search) to find the true album names and group similar ones under the exact same name.\n";

    public string CS_AIPromptCleanup_Artists => IsFr ? "- Pour les artistes: 'Artist A feat. Artist B' doit devenir 'Artist A' uniquement (garde l'artiste principal).\n- Tu DOIS vérifier en ligne (recherche web) pour t'assurer que ce sont de vrais artistes et non des noms de chaînes YouTube ou des fautes de frappe.\n" : "- For artists: 'Artist A feat. Artist B' must become 'Artist A' only (keep the main artist).\n- You MUST check online (web search) to ensure these are real artists and not YouTube channel names or typos.\n";

    public string CS_AIPromptCleanup_Genres => IsFr ? "- Pour les genres: Essayer de déduire le genre d'après l'artiste et le titre.\n- Tu DOIS vérifier en ligne (recherche web) pour trouver le vrai genre de l'artiste/chanson.\n- Si tu ne trouves rien, ou que c'est trop obscur, n'invente pas un genre, renvoie le nom exact du genre 'Unknown' ou 'Inconnu'.\n- Essaie de normaliser les genres existants (ex: 'Pop/Rock' devient 'Pop Rock', 'Hip hop' devient 'Hip-Hop').\n- Tu dois absolument regrouper au maximum les genres, il ne doit pas y avoir 50 variantes de 'Pop'.\n" : "- For genres: Try to deduce the genre from the artist and title.\n- You MUST check online (web search) to find the true genre of the artist/song.\n- If you find nothing, or it's too obscure, do not invent a genre, return the exact genre name 'Unknown'.\n- Try to normalize existing genres (e.g. 'Pop/Rock' becomes 'Pop Rock', 'Hip hop' becomes 'Hip-Hop').\n- You must absolutely group genres as much as possible, there should not be 50 variants of 'Pop'.\n";

    public string CS_AIDialog_Step1 => IsFr ? "1. Copiez les instructions ci-dessous :" : "1. Copy the instructions below:";

    public string CS_AIDialog_Step2 => IsFr ? "2. Collez-les dans ChatGPT/Claude." : "2. Paste them into ChatGPT/Claude.";

    public string CS_AIDialog_Step3 => IsFr ? "3. Copiez la réponse (le bloc JSON) et collez-la ici :" : "3. Copy the response (the JSON block) and paste it here:";

    public string CS_AIDialog_CopyBtn => IsFr ? "Copier le prompt entier (Instructions + Liste)" : "Copy full prompt (Instructions + List)";

    public string CS_AIDialog_Copied => IsFr ? "Copié !" : "Copied!";

    public string CS_Page => IsFr ? "Page" : "Page";
    public string CS_AIDialog_Placeholder => IsFr ? "Collez ici la réponse au format JSON..." : "Paste the JSON response here...";

    public string CS_AIDialog_Wait => IsFr ? "Veuillez patienter" : "Please wait";

    public string CS_AIDialog_Analyzing => IsFr ? "Analyse en cours..." : "Analyzing...";

    public string CS_AIDialog_Success => IsFr ? "Succès" : "Success";

    public string CS_AIDialog_Updated => IsFr ? "Mise à jour réussie de {0} pistes." : "Successfully updated {0} tracks.";

    public string DownloadPage_Desc1 => IsFr ? "Télécharge de la musique depuis YouTube, SoundCloud, Bandcamp et + de 1700 sites.\n" : "Download music from YouTube, SoundCloud, Bandcamp and 1700+ sites.\n";

    public string DownloadPage_Desc2 => IsFr ? " et " : " and ";

    public string DownloadPage_Desc3 => IsFr ? " sont téléchargés automatiquement si nécessaire." : " are downloaded automatically if needed.";

    public string CS_Stats_ListeningHeader => IsFr ? "Écoute" : "Listening";
    public string CS_Stats_NoTracks => IsFr ? "Aucun morceau dans la Bibliothèque." : "No track in the library.";
    public string TrackInfo_Loading => IsFr ? "Chargement des informations..." : "Loading information...";
    public string TrackInfo_NoBio => IsFr ? "Aucune biographie disponible pour cet artiste." : "No biography available for this artist.";
    public string TrackInfo_Error => IsFr ? "Erreur lors du chargement des informations : {0}" : "Error loading information: {0}";
    public string CS_Stats_NoPlays => IsFr ? "Aucune écoute enregistrée pour l'instant. Lance un morceau pour voir les statistiques ici." : "No play recorded yet. Play a track to see statistics here.";
    public string CS_Stats_ArtistPlaysFormat => IsFr ? "{0} • {1} écoutes" : "{0} • {1} plays";
    public string DownloadPage_DescText1 => IsFr ? "Télécharge de la musique depuis YouTube, SoundCloud, Bandcamp et + de 1700 sites. " : "Download music from YouTube, SoundCloud, Bandcamp and 1700+ sites. ";
    public string DownloadPage_DescText2 => IsFr ? " et " : " and ";
    public string DownloadPage_DescText3 => IsFr ? " sont téléchargés automatiquement si nécessaire." : " are downloaded automatically if needed.";
    public string CS_Stats_Tracks => IsFr ? "morceaux" : "tracks";

    public string CS_Stats_Artists => IsFr ? "artistes" : "artists";

    public string CS_Stats_Albums => IsFr ? "albums" : "albums";

    public string CS_Stats_TotalDuration => IsFr ? "Durée totale" : "total duration";

    public string CS_Stats_Plays => IsFr ? "lectures" : "plays";

    public string CS_Stats_ListeningTime => IsFr ? "temps d'écoute" : "listening time";

    public string CS_Stats_MostPlayedTrack => IsFr ? "Morceau le plus écouté" : "Most played track";

    public string CS_Stats_TopArtists => IsFr ? "Top artistes" : "Top artists";

    public string CS_Stats_TopAlbums => IsFr ? "Top albums" : "Top albums";

    public string CS_Stats_PlaysSuffix => IsFr ? "{0} écoutes" : "{0} plays";

    public string FormatTracksCount(int count) => count == 0 ? (IsFr ? "Vide" : "Empty") : (IsFr ? $"{count} morceau{(count > 1 ? "x" : "")}" : $"{count} track{(count > 1 ? "s" : "")}");
    public string FormatAlbumsCount(int count) => count == 0 ? (IsFr ? "Vide" : "Empty") : (IsFr ? $"{count} album{(count > 1 ? "s" : "")}" : $"{count} album{(count > 1 ? "s" : "")}");
    public string CS_Stats_TracksSuffix => IsFr ? "{0} morceaux" : "{0} tracks";

    public string CS_PagePrefix => IsFr ? "page" : "page";

    public string CS_AlbumsCount => IsFr ? "{0} albums" : "{0} albums";

    public string CS_ArtistsCount => IsFr ? "{0} artistes" : "{0} artists";

    public string CS_FoldersCount => IsFr ? "{0} dossiers" : "{0} folders";

    public string CS_GenresCount => IsFr ? "{0} genres" : "{0} genres";

    public string CS_Tooltip_TracksArtist => IsFr ? "Cliquer pour voir les morceaux de cet artiste" : "Click to view tracks by this artist";

    public string CS_Tooltip_TracksFolder => IsFr ? "Cliquer pour voir les morceaux de ce dossier" : "Click to view tracks in this folder";

    public string CS_Tooltip_TracksGenre => IsFr ? "Cliquer pour voir les morceaux de ce genre" : "Click to view tracks for this genre";





    public string CS_Ajouteruneplaylist => IsFr ? "Ajouter à une playlist" : "Add to playlist";

    public string CS_AlbumInconnu => IsFr ? "Album inconnu" : "Unknown album";
    public string CS_ArtisteInconnu => IsFr ? "Artiste inconnu" : "Unknown artist";
    public string CS_Album => IsFr ? "Album" : "Album";

    public string CS_Anne => IsFr ? "Année" : "Year";

    public string CS_Annuler => IsFr ? "Annuler" : "Cancel";

    public string CS_Appliquer => IsFr ? "Appliquer" : "Apply";

    public string CS_Artiste => IsFr ? "Artiste" : "Artist";

    public string CS_Aucunrsultat => IsFr ? "Aucun résultat" : "No result";

    public string CS_Autotag => IsFr ? "Autotag" : "Autotag";

    public string CS_Autotagdition => IsFr ? "Autotag - Édition" : "Autotag - Editing";

    public string CS_Changerlapochette => IsFr ? "Changer la pochette" : "Change cover";

    public string CS_Chercherenligne => IsFr ? "Chercher en ligne" : "Search online";

    public string CS_CherchersurGoogleIma => IsFr ? "Chercher sur Google Images" : "Search on Google Images";

    public string CS_Choisirunepochette => IsFr ? "Choisir une pochette" : "Choose a cover";

    public string CS_Enregistrerlesmodifi => IsFr ? "Enregistrer les modifications directement dans le fichier (écraser)" : "Save modifications directly to file (overwrite)";

    public string CS_Genre => IsFr ? "Genre" : "Genre";

    public string CS_Pages_AI_Annuler => IsFr ? "Annuler" : "Cancel";

    public string CS_Pages_AI_Appliquer => IsFr ? "Appliquer" : "Apply";

    public string CS_Piste => IsFr ? "Piste" : "Track";

    public string CS_RechercheAutotag => IsFr ? "Recherche Autotag..." : "Autotag Search...";

    public string CS_Rechercheencours => IsFr ? "Recherche en cours..." : "Searching...";

    public string CS_Rechercherunepochett => IsFr ? "Rechercher une pochette en ligne..." : "Search for cover online...";

    public string CS_Sauvegarder => IsFr ? "Sauvegarder" : "Save";

    public string CS_Services_Album => IsFr ? "Album" : "Album";

    public string CS_Services_Genre => IsFr ? "Genre" : "Genre";

    public string CS_Termederecherche => IsFr ? "Terme de recherche" : "Search term";

    public string CS_Titre => IsFr ? "Titre" : "Title";

    public string CS_Ajouterlafiledatten => IsFr ? "Ajouter à la file d'attente" : "Add to queue";

    public string CS_Voirlalbum => IsFr ? "Voir l'album" : "View album";

    public string CS_Voirlartiste => IsFr ? "Voir l'artiste" : "View artist";

    public string CS_Copierlesparoles => IsFr ? "Copier les paroles" : "Copy lyrics";

    public string CS_Recherche => IsFr ? "Recherche..." : "Searching...";
    public string CS_Settings => IsFr ? "Paramètres" : "Settings";
    public string CS_Library => IsFr ? "Bibliothèque" : "Library";
    public string CS_NoPlaylist => IsFr ? "Aucune playlist" : "No playlist";
    public string CS_ImportComplete => IsFr ? "Importation terminée" : "Import complete";
    public string CS_ImportCompleteDesc => IsFr ? "{0} playlist(s) importée(s) - {1} piste(s)." : "{0} playlist(s) imported - {1} track(s).";
    public string CS_Importing => IsFr ? "Importation en cours..." : "Importing...";
    public string CS_NewPlaylist => IsFr ? "Nouvelle playlist" : "New playlist";
    public string CS_MyPlaylist => IsFr ? "Ma Playlist" : "My Playlist";
    public string CS_Create => IsFr ? "Créer" : "Create";
    public string CS_Pages => IsFr ? "Page(s)" : "Page(s)";
    public string CS_Play => IsFr ? "Lire" : "Play";
    public string CS_Rename => IsFr ? "Renommer" : "Rename";
    public string CS_RemoveFromApp => IsFr ? "Retirer de l'application" : "Remove from app";
    public string CS_DeleteFilePermanently => IsFr ? "Supprimer le fichier définitivement" : "Delete file permanently";
    public string CS_DeleteTrackTitle => IsFr ? "Supprimer ce son ?" : "Delete this track?";
    
    public string SettingsPage_Text_DataCache => IsFr ? "Données & Cache" : "Data & Cache";
    public string SettingsPage_Text_ApplyMetadata => IsFr ? "Appliquer les métadonnées aux fichiers" : "Apply metadata to files";
    public string SettingsPage_Text_ApplyMetadataDesc => IsFr ? "Enregistre définitivement les tags et pochettes dans les fichiers originaux." : "Saves tags and covers permanently to the original files.";
    public string SettingsPage_Text_ApplyMetadataButton => IsFr ? "Appliquer" : "Apply";
    public string SettingsPage_Text_ClearCache => IsFr ? "Vider le cache" : "Clear cache";
    public string SettingsPage_Text_ClearCacheDesc => IsFr ? "Libère de l'espace en supprimant les pochettes et paroles mises en cache." : "Frees up space by deleting cached covers and lyrics.";
    public string SettingsPage_Text_ClearCacheButton => IsFr ? "Vider" : "Clear";
    
    public string CS_ClearCacheDialogTitle => IsFr ? "Vider le cache" : "Clear cache";
    public string CS_ClearCacheDialogCovers => IsFr ? "Pochettes" : "Covers";
    public string CS_ClearCacheDialogLyrics => IsFr ? "Paroles" : "Lyrics";
    
    
    public string CS_ClearCacheNormalization => IsFr ? "Normalisation" : "Normalization";
    public string CS_ClearCacheVisuallyModified => IsFr ? "Tags modifiés visuellement" : "Visually modified tags";
    public string CS_ClearCacheScannedSounds => IsFr ? "Sons scannés (bibliothèque)" : "Scanned sounds (library)";
    public string CS_ClearCacheAppSettings => IsFr ? "Paramètres de l'application" : "App settings";
    public string CS_ClearCacheAll => IsFr ? "Tout effacer (supprime le cache complet)" : "Clear all (deletes entire cache folder)";

    public string CS_RestartRequiredTitle => IsFr ? "Redémarrage requis" : "Restart required";
    public string CS_RestartRequiredBody => IsFr ? "Veuillez relancer l'application pour appliquer ces changements." : "Please restart the application to apply these changes.";
public string CS_ApplyMetadataDialogTitle => IsFr ? "Appliquer les métadonnées" : "Apply metadata";
    public string CS_ApplyMetadataDialogTags => IsFr ? "Tags (Titre, Artiste, etc.)" : "Tags (Title, Artist, etc.)";
    public string CS_ApplyMetadataDialogProgress => IsFr ? "Traitement en cours..." : "Processing...";
public string CS_DeleteTracksTitle => IsFr ? "Supprimer ces sons ?" : "Delete these tracks?";
    public string CS_DeleteTrackBody => IsFr ? "Choisissez comment supprimer ce son." : "Choose how to delete this track.";
    public string CS_DeleteTracksBody => IsFr ? "Choisissez comment supprimer ces {0} sons." : "Choose how to delete these {0} tracks.";
    public string CS_NowPlayingOptions => IsFr ? "Options du son en cours" : "Now playing options";
    public string CS_Delete => IsFr ? "Supprimer" : "Delete";
    public string CS_NoPlaylistInCategory => IsFr ? "Aucune playlist. Importez un fichier .m3u/.m3u8 ou créez une playlist manuellement." : "No playlist. Import an .m3u/.m3u8 file or create a playlist manually.";




    public string AlbumsPage_Text_Albums => IsFr ? "Albums" : "Albums";
    public string AlbumsPage_Text_NettoyageIA => IsFr ? "Nettoyage IA" : "AI Cleanup";
    public string AlbumsPage_Text_Rechercher => IsFr ? "Rechercher" : "Search";
    public string ArtistsPage_Text_Artistes => IsFr ? "Artistes" : "Artists";
    public string ArtistsPage_Text_NettoyageIA => IsFr ? "Nettoyage IA" : "AI Cleanup";
    public string ArtistsPage_Text_Rechercher => IsFr ? "Rechercher" : "Search";
    public string DownloadPage_Content_Coller => IsFr ? "Coller" : "Paste";
    public string DownloadPage_Content_Rechercher => IsFr ? "Rechercher" : "Search";
    public string DownloadPage_Text_AppliquerAutotag => IsFr ? "Appliquer Autotag" : "Apply Autotag";
    public string DownloadPage_Text_ArtisteTitreexDaftPu => IsFr ? "Artiste - Titre (ex: Daft Punk)" : "Artist - Title (e.g. Daft Punk)";
    public string DownloadPage_Text_Lasortiedeytdlpappar => IsFr ? "La sortie apparaîtra ici..." : "Output will appear here...";
    public string DownloadPage_Text_RechercheYouTube => IsFr ? "Recherche YouTube" : "YouTube Search";
    public string DownloadPage_Text_Tlcharger => IsFr ? "Télécharger" : "Download";
    public string DownloadPage_Text_Tlchargerdelamusique => IsFr ? "Téléchargement" : "Download";
    public string DownloadPage_Text_URL => IsFr ? "URL" : "URL";
    public string DownloadPage_Text_ffmpeg => IsFr ? "ffmpeg" : "ffmpeg";
    public string DownloadPage_Text_httpswwwyoutubecomwa => IsFr ? "https://www.youtube.com/watch?v=..." : "https://www.youtube.com/watch?v=...";
    public string DownloadPage_Text_ytdlp => IsFr ? "yt-dlp" : "yt-dlp";
    public string FoldersPage_Text_Dossiers => IsFr ? "Dossiers" : "Folders";
    public string FoldersPage_Text_Rechercher => IsFr ? "Rechercher" : "Search";
    public string GenresPage_Text_Genres => IsFr ? "Genres" : "Genres";
    public string GenresPage_Text_NettoyageIA => IsFr ? "Nettoyage IA" : "AI Cleanup";
    public string GenresPage_Text_Rechercher => IsFr ? "Rechercher" : "Search";
    public string GlobalSettings => IsFr ? "Paramètres globaux" : "Global Settings";
    public string LibraryPage_Content_1000morceaux => IsFr ? "1000 morceaux" : "1000 tracks";
    public string LibraryPage_Content_100morceaux => IsFr ? "100 morceaux" : "100 tracks";
    public string LibraryPage_Content_2000morceaux => IsFr ? "2000 morceaux" : "2000 tracks";
    public string LibraryPage_Content_300morceaux => IsFr ? "300 morceaux" : "300 tracks";
    public string LibraryPage_Content_500morceaux => IsFr ? "500 morceaux" : "500 tracks";
    public string LibraryPage_Content_50morceaux => IsFr ? "50 morceaux" : "50 tracks";
    public string LibraryPage_Content_Toutafficher => IsFr ? "Tout afficher" : "Show all";
    public string LibraryPage_Text_Ajoutrcentdabord => IsFr ? "Ajout récent en premier" : "Recently added first";
    public string LibraryPage_Text_Alatoire => IsFr ? "Aléatoire" : "Shuffle";
    public string LibraryPage_Text_AlbumAgtZ => IsFr ? "Album (A-Z)" : "Album (A-Z)";
    public string LibraryPage_Text_ArtisteAgtZ => IsFr ? "Artiste (A-Z)" : "Artist (A-Z)";
    public string LibraryPage_Text_BIBLIOTHQUE => IsFr ? "Bibliothèque" : "Library";
    public string LibraryPage_Text_Durecroissant => IsFr ? "Durée (croissant)" : "Duration (ascending)";
    public string LibraryPage_Text_Duredcroissant => IsFr ? "Durée (décroissant)" : "Duration (descending)";
    public string LibraryPage_Text_RechercherdanslaBIBL => IsFr ? "Rechercher dans la Bibliothèque" : "Search in library";
    public string LibraryPage_Text_TitreAgtZ => IsFr ? "Titre (A-Z)" : "Title (A-Z)";
    public string LibraryPage_Text_TrierArtiste => IsFr ? "Trier par Artiste" : "Sort by Artist";
    public string LibraryPage_ToolTipService_ToolTip_LectureAlatoire => IsFr ? "Lecture aléatoire" : "Shuffle playback";
    public string LibraryPage_ToolTipService_ToolTip_Trierlaliste => IsFr ? "Trier la liste" : "Sort list";
    public string LyricsPage_Text_Lancelalecturedunmor => IsFr ? "Lance la lecture d'un morceau pour voir les paroles." : "Play a track to see lyrics.";
    public string LyricsPage_Text_Paroles => IsFr ? "Paroles" : "Lyrics";
    public string MainWindow_Content_Albums => IsFr ? "Albums" : "Albums";
    public string MainWindow_Content_Artistes => IsFr ? "Artistes" : "Artists";
    public string MainWindow_Content_Bibliothque => IsFr ? "Bibliothèque" : "Library";
    public string MainWindow_Content_Dossiers => IsFr ? "Dossiers" : "Folders";
    public string MainWindow_Content_Filedattente => IsFr ? "File d'attente" : "Queue";
    public string MainWindow_Content_Genres => IsFr ? "Genres" : "Genres";
    public string MainWindow_Content_Playlists => IsFr ? "Playlists" : "Playlists";
    public string MainWindow_Content_Statistiques => IsFr ? "Statistiques" : "Statistics";
    public string MainWindow_Content_Tlchargement => IsFr ? "Téléchargement" : "Download";
    public string MainWindow_Text_000 => IsFr ? "0:00" : "0:00";
    public string MainWindow_Text_Aucunelecture => IsFr ? "Aucune lecture" : "No playback";
    public string MainWindow_Text_Resona => IsFr ? "Resona" : "Resona";
    public string MainWindow_Text_Paroles => IsFr ? "Paroles" : "Lyrics";
    public string MainWindow_ToolTipService_ToolTip_Modedelecturelecture => IsFr ? "Mode de lecture" : "Playback mode";

    public string OnboardingPage_Content_Choisirundossierdemu => IsFr ? "Choisir un dossier de musique" : "Choose a music folder";
    public string OnboardingPage_Content_Passercettetape => IsFr ? "Passer cette étape" : "Skip this step";
    public string OnboardingPage_Text_BienvenuedansAudioPl => IsFr ? "Bienvenue dans Resona" : "Welcome to Resona";
    public string OnboardingPage_Text_Pourcommencerchoisis => IsFr ? "Pour commencer, choisis un dossier contenant ta musique." : "To begin, choose a folder containing your music.";
    public string PlaylistDetailPage_Text_Toutlire => IsFr ? "Tout lire" : "Play all";
    public string PlaylistDetailPage_ToolTipService_ToolTip_Exporterm3u8 => IsFr ? "Exporter (.m3u8)" : "Export (.m3u8)";
    public string PlaylistDetailPage_ToolTipService_ToolTip_Renommer => IsFr ? "Renommer" : "Rename";
    public string PlaylistDetailPage_ToolTipService_ToolTip_Supprimer => IsFr ? "Supprimer" : "Delete";
    public string PlaylistsPage_Text_Ajoutrcent => IsFr ? "Ajout récent" : "Recently added";
    public string PlaylistsPage_Text_Gnreruneplaylistavec => IsFr ? "Générer avec l'IA" : "Generate with AI";
    public string PlaylistsPage_Text_Importerm3um3u8 => IsFr ? "Importer (.m3u / .m3u8)" : "Import (.m3u / .m3u8)";
    public string PlaylistsPage_Text_NomAgtZ => IsFr ? "Nom (A-Z)" : "Name (A-Z)";
    public string PlaylistsPage_Text_NomZgtA => IsFr ? "Nom (Z-A)" : "Name (Z-A)";
    public string PlaylistsPage_Text_Nombredesons => IsFr ? "Nombre de sons" : "Number of tracks";
    public string PlaylistsPage_Text_Nouvelleplaylist => IsFr ? "Nouvelle playlist" : "New playlist";
    public string PlaylistsPage_Text_Playlists => IsFr ? "Playlists" : "Playlists";
    public string PlaylistsPage_Text_TrierNom => IsFr ? "Trier par Nom" : "Sort by Name";
    public string PlaylistsPage_ToolTipService_ToolTip_Exporterm3u8 => IsFr ? "Exporter (.m3u8)" : "Export (.m3u8)";
    public string PlaylistsPage_ToolTipService_ToolTip_Renommer => IsFr ? "Renommer" : "Rename";
    public string PlaylistsPage_ToolTipService_ToolTip_Supprimer => IsFr ? "Supprimer" : "Delete";
    public string PlaylistsPage_ToolTipService_ToolTip_Toutlire => IsFr ? "Tout lire" : "Play all";
    public string PlaylistsPage_ToolTipService_ToolTip_Trierlesplaylists => IsFr ? "Trier les playlists" : "Sort playlists";
    public string QueuePage_Text_Filedattente => IsFr ? "File d'attente" : "Queue";
    public string SettingsLanguageDesc => IsFr ? "Change la langue de l'application" : "Change the application language";
    public string SettingsLanguageTitle => IsFr ? "Langue" : "Language";
    public string NowPlayingPage_Text_EnLecture => IsFr ? "En lecture" : "Now Playing";
    public string NowPlayingPage_Text_Artiste => IsFr ? "Artiste" : "Artist";
    public string SettingsPage_Toggle_AutoNowPlaying => IsFr ? "Ouvrir l'affichage de lecture automatiquement" : "Auto-open Now Playing view";
    public string SettingsPage_Text_AutoNowPlayingDesc => IsFr ? "Basculer sur la pochette en grand au lancement d'un titre." : "Switch to large cover view when playing a track.";
    public string SettingsPage_Content_128kbps => IsFr ? "128 kbps" : "128 kbps";
    public string SettingsPage_Content_192kbps => IsFr ? "192 kbps" : "192 kbps";
    public string SettingsPage_Content_256kbps => IsFr ? "256 kbps" : "256 kbps";
    public string SettingsPage_Content_320kbps => IsFr ? "320 kbps" : "320 kbps";
    public string SettingsPage_Content_Acryliceffetverredpo => IsFr ? "Acrylic (effet verre dépoli)" : "Acrylic (frosted glass effect)";
    public string SettingsPage_Content_Activ => IsFr ? "Activé" : "Enabled";
    public string SettingsPage_Content_Ajouterundossierdemu => IsFr ? "Ajouter un dossier de musique" : "Add a music folder";
    public string SettingsPage_Content_Albums => IsFr ? "Albums" : "Albums";
    public string SettingsPage_Content_Artistes => IsFr ? "Artistes" : "Artists";
    public string SettingsPage_Content_Bibliothque => IsFr ? "Bibliothèque" : "Library";
    public string SettingsPage_Content_Choisir => IsFr ? "Choisir" : "Choose";
    public string SettingsPage_Content_Couleurunie => IsFr ? "Couleur unie" : "Solid color";
    public string SettingsPage_Content_Dossiers => IsFr ? "Dossiers" : "Folders";
    public string SettingsPage_Content_Dsactiv => IsFr ? "Désactivé" : "Disabled";
    public string SettingsPage_Content_Englishen => IsFr ? "English (en)" : "English (en)";
    public string SettingsPage_Content_Exporterlabibliothqu => IsFr ? "Exporter toutes les playlists" : "Export all playlists";
    public string SettingsPage_Content_FLACsanspertegrosfic => IsFr ? "FLAC (sans perte)" : "FLAC (lossless)";
    public string SettingsPage_Content_Franaisfr => IsFr ? "Français (fr)" : "Français (fr)";
    public string SettingsPage_Content_Genres => IsFr ? "Genres" : "Genres";
    public string SettingsPage_Content_Importeruneplaylistm => IsFr ? "Importer une playlist (.m3u)" : "Import a playlist (.m3u)";
    public string SettingsPage_Content_M4AAAC => IsFr ? "M4A (AAC)" : "M4A (AAC)";
    public string SettingsPage_Content_MP3compatibleunivers => IsFr ? "MP3 (compatible universellement)" : "MP3 (universally compatible)";
    public string SettingsPage_Content_Meilleurequalitsourc => IsFr ? "Meilleure qualité source (WAV)" : "Best source quality (WAV)";
    public string SettingsPage_Content_MicaAltplussombre => IsFr ? "Mica Alt (plus sombre)" : "Mica Alt (darker)";
    public string SettingsPage_Content_Micarecommand => IsFr ? "Mica (recommandé)" : "Mica (recommended)";
    public string SettingsPage_Content_Opusrecommandmeilleu => IsFr ? "Opus (recommandé)" : "Opus (recommended)";
    public string SettingsPage_Content_Playlists => IsFr ? "Playlists" : "Playlists";
    public string SettingsPage_Content_Restaurerlesancienst => IsFr ? "Restaurer" : "Restore";
    public string SettingsPage_Content_Statistiquesdcoute => IsFr ? "Statistiques d'écoute" : "Listening statistics";
    public string SettingsPage_Content_Tlchargement => IsFr ? "Téléchargement" : "Download";
    public string SettingsPage_Content_VorbisOGG => IsFr ? "Vorbis (OGG)" : "Vorbis (OGG)";
    public string SettingsPage_Content_WAVnoncompress => IsFr ? "WAV (non compressé)" : "WAV (uncompressed)";
    public string SettingsPage_Header_Activerleboutondelas => IsFr ? "Activer le bouton" : "Enable button";
    public string SettingsPage_Header_Activerlgaliseur => IsFr ? "Activer l'égaliseur" : "Enable equalizer";
    public string SettingsPage_Header_Boutonderecherchedes => IsFr ? "Bouton de recherche des paroles" : "Lyrics search button";
    public string SettingsPage_Header_Dbordementdugradient => IsFr ? "Débordement du gradient" : "Gradient overflow";
    public string SettingsPage_Header_DmarreravecWindows => IsFr ? "Démarrer avec Windows" : "Start with Windows";
    public string SettingsPage_Header_Dmarrerdirectementen => IsFr ? "Démarrer réduit (zone de notification)" : "Start minimized (system tray)";
    public string SettingsPage_Header_Modeaudioexclusifqua => IsFr ? "Mode audio exclusif" : "Exclusive audio mode";
    public string SettingsPage_Header_Normalisationduvolum => IsFr ? "Normalisation du volume" : "Volume normalization";
    public string SettingsPage_Header_Rduiredanslabarredes => IsFr ? "Réduire dans la barre des tâches" : "Minimize to taskbar";
    public string SettingsPage_Header_Rechercheautomatique => IsFr ? "Recherche automatique de pochettes" : "Automatic cover search";
    public string SettingsPage_Text_RechercheautomatiqueDesc => IsFr ? "Télécharge automatiquement les pochettes manquantes depuis Internet lors de l'ajout de nouveaux morceaux." : "Automatically downloads missing cover art from the internet when adding new tracks.";
    public string SettingsPage_Header_Traductiondesparoles => IsFr ? "Traduction des paroles" : "Lyrics translation";
    public string SettingsPage_Text_Ajusteautomatiquemen => IsFr ? "Ajuste automatiquement le volume pour que tous les morceaux soient au même niveau sonore." : "Automatically adjusts the volume so all tracks are at the same audio level.";
    public string SettingsPage_Text_AjustelegaindBdechaq => IsFr ? "Ajuste le gain (dB) de chaque bande de fréquence pour modifier le rendu sonore." : "Adjusts the gain (dB) of each frequency band to change the sound output.";
    public string SettingsPage_Text_Apparence => IsFr ? "Apparence" : "Appearance";
    public string SettingsPage_Text_AssistantPrompt => IsFr ? "Assistant Prompt" : "Prompt Assistant";
    public string SettingsPage_Text_Bibliothquemusicale => IsFr ? "Bibliothèque musicale" : "Music Library";
    public string SettingsPage_Text_Catgoriesaffichesdan => IsFr ? "Catégories affichées" : "Displayed categories";
    public string SettingsPage_Text_Codecoptionnel => IsFr ? "Codec (optionnel)" : "Codec (optional)";
    public string SettingsPage_Text_Couleurduthme => IsFr ? "Couleur du thème" : "Theme color";
    public string SettingsPage_Text_DonneResonauna => IsFr ? "Donne à Resona un accès exclusif au périphérique audio (contourne le mixeur de volume de Windows, ce qui peut bloquer le son des autres applications)." : "Gives Resona exclusive access to the audio device (bypasses Windows volume mixer, which may prevent other apps from playing sound).";
    public string SettingsPage_Text_Dossierdedestination => IsFr ? "Dossier de destination" : "Destination folder";
    public string SettingsPage_Text_Dossierdetlchargemen => IsFr ? "Dossier de téléchargement" : "Download folder";
    public string SettingsPage_Text_Exempleslibopuslibmp => IsFr ? "Exemples : libopus, libmp3lame, aac, copy. Laisse vide pour le choix par défaut du format." : "Examples: libopus, libmp3lame, aac, copy. Leave empty for default format choice.";
    public string SettingsPage_Text_Fonctionnalits => IsFr ? "Fonctionnalités" : "Features";
    public string SettingsPage_Text_Formatdufichier => IsFr ? "Format du fichier" : "File format";
    public string SettingsPage_Text_Laissvidecodecnatifd => IsFr ? "Laisse vide (codec natif du site) ou force un encodage :" : "Leave empty (native codec) or force encoding:";
    public string SettingsPage_Text_Lapplicationserduitd => IsFr ? "L'application se réduit dans la zone de notification au lieu de se fermer." : "The application minimizes to the notification area instead of closing.";
    public string SettingsPage_Text_Legradientdbordeaude => IsFr ? "Le gradient déborde au-dessus du lecteur pour un effet visuel plus immersif." : "The gradient overflows above the player for a more immersive visual effect.";
    public string SettingsPage_Text_Meilleurequalitsourc => IsFr ? "Meilleure qualité source (WAV)" : "Best source quality (WAV)";
    public string SettingsPage_Text_NcessiteRduireenbarr => IsFr ? "Nécessite d'activer l'option 'Réduire en barre des tâches à la fermeture'." : "Requires enabling the 'Minimize to tray on close' option.";
    public string SettingsPage_Text_Playlists => IsFr ? "Playlists" : "Playlists";
    public string SettingsPage_Text_SiActivunboutonParol => IsFr ? "Si Activé, un bouton \"Paroles\" apparaît." : "If Enabled, a \"Lyrics\" button appears.";
    public string SettingsPage_Text_Styledefonddefentre => IsFr ? "Style de fond de fenêtre" : "Window background style";
    public string SettingsPage_Text_Systme => IsFr ? "Système" : "System";
    public string SettingsPage_Text_Tlchargement => IsFr ? "Téléchargement" : "Download";
    public string SettingsPage_Text_Traduitautomatiqueme => IsFr ? "Traduit automatiquement les paroles dans votre langue si elles sont dans une autre langue." : "Automatically translates lyrics into your language if they are in another language.";
    public string SettingsPage_Text_galiseur => IsFr ? "Égaliseur" : "Equalizer";
    public string SettingsPage_Text_qualitdbitcible => IsFr ? "Qualité / Débit cible" : "Quality / Target bitrate";
    public string StatisticsPage_Text_Statistiques => IsFr ? "Statistiques" : "Statistics";

    public string SettingsPage_Text_Updates => IsFr ? "Mises à jour" : "Updates";
    public string SettingsPage_Text_AutoUpdate => IsFr ? "Vérifier les mises à jour au lancement" : "Check for updates on startup";
    public string SettingsPage_Text_ClearCovers => IsFr ? "Vider le cache des pochettes automatiques" : "Clear automatic cover cache";
    public string SettingsPage_Text_CheckUpdate => IsFr ? "Rechercher une mise à jour" : "Check for updates";
    public string Update_NewVersion_Title => IsFr ? "Nouvelle mise à jour" : "New update available";
    public string Update_NewVersion_Message => IsFr ? "Une nouvelle version de Resona est disponible. Voulez-vous la télécharger ?" : "A new version of Resona is available. Do you want to download it?";
    public string Update_Download => IsFr ? "Télécharger" : "Download";
    public string Update_Close => IsFr ? "Fermer" : "Close";
    public string Update_UpToDate_Title => IsFr ? "Vous êtes à jour" : "You are up to date";
    public string Update_UpToDate_Message => IsFr ? "Vous possédez déjà la dernière version de Resona." : "You already have the latest version of Resona.";

}
