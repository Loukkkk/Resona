using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Services;

/// <summary>
/// Représente une piste extraite d'un fichier d'export de playlist externe (Spotify CSV, Soundiiz, TuneMyMusic, TXT, etc.).
/// </summary>
public sealed class PlaylistImportEntry
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public TimeSpan? Duration { get; set; }
    public string RawLine { get; set; } = string.Empty;

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(Artist))
            return $"\"{Title}\" - {Artist}";
        return Title;
    }
}

/// <summary>
/// Parseur universel de fichiers d'exportation de playlists (CSV Spotify/Exportify, Soundiiz, TuneMyMusic, TSV, TXT).
/// </summary>
public static class PlaylistExportParser
{
    // "Artiste - Titre" : le tiret doit être entouré d'espaces (sinon "Jay-Z", "Spider-Man" seraient coupés).
    private static readonly Regex SpacedDash = new(@"^(?<a>.+?)\s+[-–—−]\s+(?<b>.+)$", RegexOptions.Compiled);
    // "Titre by Artiste" : "by" doit être un mot entier (sinon "Baby" serait coupé en "Ba" + "by").
    private static readonly Regex ByArtist = new(@"^(?<t>.+?)\s+by\s+(?<a>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingNumber = new(@"^\s*\d{1,3}\s*[\.\)]\s+|^\s*\d{1,3}\s+[-–—]\s+", RegexOptions.Compiled);

    /// <summary>
    /// Lit et analyse un fichier d'export de playlist pour en extraire la liste des pistes.
    /// </summary>
    public static List<PlaylistImportEntry> ParseFile(string filePath)
    {
        var entries = new List<PlaylistImportEntry>();
        if (!File.Exists(filePath)) return entries;

        string text = ReadAllTextSmart(filePath);
        if (string.IsNullOrWhiteSpace(text)) return entries;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        bool delimitedExtension = ext is ".csv" or ".tsv";

        // CSV / TSV : toujours essayé. Autre fichier (ex. .txt) : seulement si la première ligne est un en-tête reconnu,
        // pour ne pas prendre « Earth, Wind & Fire - September » pour une ligne de CSV.
        string firstLine = FirstNonEmptyLine(text);
        char delimiter = DetectDelimiter(firstLine);
        bool headerLooksRight = FindColumns(ParseCsvLine(firstLine, delimiter)).Title >= 0;

        if (delimitedExtension || headerLooksRight)
        {
            entries = ParseRecords(ReadRecords(text, delimiter));
            if (entries.Count > 0) return entries;
        }

        return ParseTextLines(text);
    }

    // ------------------------------------------------------------------
    //  Lecture du fichier
    // ------------------------------------------------------------------

    /// <summary>UTF-8 (avec ou sans BOM), UTF-16, sinon Windows-1252 (export Excel) au lieu de perdre les accents.</summary>
    private static string ReadAllTextSmart(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        string text;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        else
        {
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);   // strict : lève une exception si ce n'est pas de l'UTF-8
            }
            catch (DecoderFallbackException)
            {
                Encoding legacy;
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    legacy = Encoding.GetEncoding(1252);
                }
                catch { legacy = Encoding.Latin1; }
                text = legacy.GetString(bytes);
            }
        }
        return text.TrimStart('\uFEFF');
    }

    private static string FirstNonEmptyLine(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            string t = line.Trim();
            if (t.Length > 0) return t;
        }
        return string.Empty;
    }

    // ------------------------------------------------------------------
    //  CSV / TSV
    // ------------------------------------------------------------------

    private static (int Title, int Artist, int Album, int Duration, bool DurationMs) FindColumns(IReadOnlyList<string> headers)
    {
        int title = -1, artist = -1, album = -1, duration = -1;
        bool ms = false;

        for (int i = 0; i < headers.Count; i++)
        {
            string h = NormalizeHeaderName(headers[i]);
            if (title < 0 && h is "trackname" or "tracktitle" or "track" or "title" or "titre" or "song" or "songname" or "name" or "nom" or "chanson" or "piste")
            {
                title = i;
            }
            else if (artist < 0 && (h.StartsWith("artist") || h is "performer" or "interp" or "interprete" or "auteur" or "author"))
            {
                artist = i;
            }
            else if (album < 0 && (h.StartsWith("album") && !h.Contains("artist") || h is "disque" or "record"))
            {
                album = i;
            }
            else if (duration < 0 && (h.StartsWith("duration") || h.StartsWith("duree") || h is "length" or "time"))
            {
                duration = i;
                ms = h.EndsWith("ms") || h.Contains("millis");
            }
        }
        return (title, artist, album, duration, ms);
    }

    private static List<PlaylistImportEntry> ParseRecords(List<List<string>> records)
    {
        var entries = new List<PlaylistImportEntry>();
        if (records.Count == 0) return entries;

        var cols = FindColumns(records[0]);
        int startRow = 1;

        if (cols.Title < 0)
        {
            // Pas d'en-tête reconnu : colonnes supposées « Titre, Artiste » (le matcher essaie aussi l'inverse).
            // Une seule colonne : ce n'est pas un tableau, on laisse la lecture « texte » s'en charger.
            if (records[0].Count < 2) return entries;
            startRow = 0;
            cols = (0, 1, -1, -1, false);
        }

        for (int r = startRow; r < records.Count; r++)
        {
            var row = records[r];
            if (cols.Title >= row.Count) continue;

            string title = row[cols.Title].Trim();
            if (title.Length == 0) continue;

            string artist = cols.Artist >= 0 && cols.Artist < row.Count ? row[cols.Artist].Trim() : string.Empty;
            string? album = cols.Album >= 0 && cols.Album < row.Count && row[cols.Album].Trim().Length > 0 ? row[cols.Album].Trim() : null;
            TimeSpan? duration = cols.Duration >= 0 && cols.Duration < row.Count ? ParseDuration(row[cols.Duration], cols.DurationMs) : null;

            entries.Add(new PlaylistImportEntry
            {
                Title = title,
                Artist = artist,
                Album = album,
                Duration = duration,
                RawLine = string.Join(";", row)
            });
        }
        return entries;
    }

    /// <summary>Lecture CSV complète : champs entre guillemets (virgules, guillemets doublés « "" » et retours à la ligne dedans).</summary>
    private static List<List<string>> ReadRecords(string text, char delimiter)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        bool fieldStarted = false;   // du contenu a déjà été lu dans ce champ : un guillemet n'ouvre alors plus un champ entre guillemets

        void EndRecord()
        {
            fields.Add(sb.ToString().Trim());
            sb.Clear();
            fieldStarted = false;
            if (fields.Count > 1 || fields[0].Length > 0) records.Add(fields);
            fields = new List<string>();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
                continue;
            }

            if (c == '"' && !fieldStarted) { inQuotes = true; fieldStarted = true; continue; }
            if (c == delimiter)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
                fieldStarted = false;
                continue;
            }
            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRecord();
                continue;
            }
            sb.Append(c);
            if (!char.IsWhiteSpace(c)) fieldStarted = true;
        }
        if (sb.Length > 0 || fields.Count > 0) EndRecord();
        return records;
    }

    public static List<string> ParseCsvLine(string line, char delimiter)
    {
        if (line == null) return new List<string>();
        var records = ReadRecords(line, delimiter);
        return records.Count > 0 ? records[0] : new List<string>();
    }

    private static string NormalizeHeaderName(string header)
    {
        return header.Trim().Trim('\uFEFF', '"').ToLowerInvariant()
            .Replace("_", "")
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    private static char DetectDelimiter(string headerLine)
    {
        int comma = 0, semicolon = 0, tab = 0;
        bool inQuotes = false;
        foreach (char c in headerLine)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes)
            {
                if (c == ',') comma++;
                else if (c == ';') semicolon++;
                else if (c == '\t') tab++;
            }
        }
        if (tab > comma && tab > semicolon) return '\t';
        if (semicolon > comma) return ';';
        return ',';
    }

    /// <summary>
    /// Durée : « 3:45 » ou « 1:02:03 » (minutes:secondes, PAS heures:minutes), sinon un nombre. Un nombre est en millisecondes
    /// si l'en-tête le dit (« Duration (ms) ») ou s'il est grand (≥ 10 000), en secondes sinon.
    /// </summary>
    private static TimeSpan? ParseDuration(string val, bool headerIsMs)
    {
        val = (val ?? string.Empty).Trim();
        if (val.Length == 0) return null;

        if (val.Contains(':'))
        {
            string[] parts = val.Split(':');
            if (parts.Length is < 2 or > 3) return null;
            double total = 0;
            foreach (string p in parts)
            {
                if (!double.TryParse(p.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v < 0) return null;
                total = total * 60 + v;
            }
            return TimeSpan.FromSeconds(total);
        }

        if (double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && n > 0)
        {
            return headerIsMs || n >= 10000 ? TimeSpan.FromMilliseconds(n) : TimeSpan.FromSeconds(n);
        }
        return null;
    }

    // ------------------------------------------------------------------
    //  Texte simple : une piste par ligne
    // ------------------------------------------------------------------

    private static string Unquote(string s) => s.Trim().Trim('"', '\'', '“', '”', '«', '»').Trim();

    private static List<PlaylistImportEntry> ParseTextLines(string text)
    {
        var entries = new List<PlaylistImportEntry>();

        foreach (string rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

            // Numéro de piste initial : « 1. », « 02) », « 03 - » (mais pas « 99 Problems » ni « 7 Rings »).
            string clean = LeadingNumber.Replace(line, string.Empty).Trim();
            if (clean.Length == 0) continue;

            Match dash = SpacedDash.Match(clean);
            if (dash.Success)
            {
                string a = dash.Groups["a"].Value.Trim();
                string b = dash.Groups["b"].Value.Trim();
                // Convention « Artiste - Titre » ; « "Titre" - Artiste » (format de la liste des introuvables) est reconnu aussi.
                // Le matcher essaie de toute façon l'ordre inverse.
                bool quotedFirst = a.Length >= 2 && (a[0] == '"' || a[0] == '“' || a[0] == '«');
                entries.Add(new PlaylistImportEntry
                {
                    Title = Unquote(quotedFirst ? a : b),
                    Artist = Unquote(quotedFirst ? b : a),
                    RawLine = line
                });
                continue;
            }

            Match by = ByArtist.Match(clean);
            if (by.Success)
            {
                entries.Add(new PlaylistImportEntry { Title = Unquote(by.Groups["t"].Value), Artist = Unquote(by.Groups["a"].Value), RawLine = line });
                continue;
            }

            entries.Add(new PlaylistImportEntry { Title = Unquote(clean), RawLine = line });
        }
        return entries;
    }
}

/// <summary>
/// Moteur de correspondance floue : compare les pistes importées à la bibliothèque locale.
/// Un titre identique avec un autre artiste n'est jamais accepté ; les correspondances moyennes (&lt; ConfidentScore)
/// sont signalées pour que l'interface les fasse relire.
/// </summary>
public sealed class FuzzyTrackMatcher
{
    /// <summary>Score minimal pour proposer une correspondance.</summary>
    public const double AcceptScore = 0.70;
    /// <summary>Au-dessus : correspondance sûre. Entre les deux : à faire relire.</summary>
    public const double ConfidentScore = 0.85;

    private const int MaxCandidates = 300;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "feat", "ft", "with", "version", "remaster", "remastered", "live", "edit", "mix", "radio", "from",
        "les", "des", "une", "que", "pour", "dans", "par", "sur", "aux", "est", "pas", "mon", "ton", "son", "tes", "ses",
        "los", "las", "del", "und", "der", "die", "das"
    };

    // Parenthèses / crochets qui ne décrivent que la version ou les invités : (feat. X), [Remastered 2011], (Live), (Radio Edit)...
    // Volontairement ABSENTS : remix, acoustic, instrumental (ce sont d'autres morceaux).
    private static readonly Regex BracketNoise = new(
        @"[\(\[\{][^\)\]\}]*?\b(?:feat|ft|featuring|with|remaster(?:ed)?|live|version|edit|mono|stereo|explicit|bonus|deluxe|anniversary|radio|single|album|official|audio|video|clip|lyrics?|from)\b[^\)\]\}]*[\)\]\}]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrailingNoise = new(
        @"\s+[-–—]\s+(?:(?:\d{4}\s+)?(?:digital\s+)?remaster(?:ed)?(?:\s+\d{4})?|live\b.*|radio\s+edit|single\s+version|album\s+version|(?:mono|stereo)(?:\s+(?:version|mix))?|bonus\s+track|explicit|official\b.*|from\b.*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FeatTail = new(@"\s+(?:feat\.?|ft\.?|featuring)\s+.*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ArtistSplit = new(
        @"\s*[;,&+]\s*|\s+/\s+|\s+(?:feat\.?|ft\.?|featuring|with|vs\.?|x|and|et)\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SpacedDash = new(@"^(?<a>.+?)\s+[-–—−]\s+(?<b>.+)$", RegexOptions.Compiled);

    private sealed class Item
    {
        public Track Track = null!;
        public string TitleFull = string.Empty;
        public string TitleCore = string.Empty;
        public string ArtistFull = string.Empty;       // vide = artiste inconnu
        public List<string> ArtistParts = new();
        public string Album = string.Empty;
        public HashSet<string> Tokens = new();         // mots du titre (cœur) + de l'artiste
    }

    private readonly List<Item> _items = new();
    private readonly Dictionary<string, List<Item>> _titleIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Item>> _wordIndex = new(StringComparer.Ordinal);

    public FuzzyTrackMatcher(IEnumerable<Track> tracks)
    {
        foreach (Track t in tracks)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.FilePath)) continue;
            _items.Add(BuildItem(t));
        }
        foreach (Item it in _items) Index(it);
    }

    private static Item BuildItem(Track t)
    {
        string titleFull = NormalizeText(t.Title);
        string titleCore = NormalizeText(StripNoiseAndParentheses(t.Title ?? string.Empty));
        if (titleCore.Length == 0) titleCore = titleFull;

        string artistFull = NormalizeText(t.Artist);
        if (artistFull is "unknown artist" or "unknown" or "artiste inconnu" or "inconnu") artistFull = string.Empty;

        var item = new Item
        {
            Track = t,
            TitleFull = titleFull,
            TitleCore = titleCore,
            ArtistFull = artistFull,
            ArtistParts = artistFull.Length == 0 ? new List<string>() : SplitArtists(t.Artist),
            Album = NormalizeText(t.Album)
        };
        item.Tokens = Tokens(titleCore + " " + artistFull);
        return item;
    }

    private void Index(Item it)
    {
        AddTo(_titleIndex, it.TitleFull, it);
        if (it.TitleCore != it.TitleFull) AddTo(_titleIndex, it.TitleCore, it);
        foreach (string w in IndexWords(it.TitleCore, it.ArtistParts)) AddTo(_wordIndex, w, it);
    }

    private static void AddTo(Dictionary<string, List<Item>> index, string key, Item it)
    {
        if (key.Length == 0) return;
        if (!index.TryGetValue(key, out var list)) index[key] = list = new List<Item>();
        list.Add(it);
    }

    private static IEnumerable<string> IndexWords(string titleCore, List<string> artistParts)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (string w in titleCore.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (w.Length >= 3 && !StopWords.Contains(w)) words.Add(w);
        foreach (string part in artistParts)
            foreach (string w in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (w.Length >= 3 && !StopWords.Contains(w)) words.Add(w);
        return words;
    }

    // ------------------------------------------------------------------
    //  Recherche
    // ------------------------------------------------------------------

    /// <summary>
    /// Piste la plus proche pour une entrée. Renvoie (piste, score) si le score atteint AcceptScore, sinon (null, meilleur score).
    /// En dessous de ConfidentScore la correspondance est « à relire ».
    /// </summary>
    public (Track? matchedTrack, double score) Match(PlaylistImportEntry entry)
    {
        if (entry == null || (string.IsNullOrWhiteSpace(entry.Title) && string.IsNullOrWhiteSpace(entry.Artist)))
            return (null, 0.0);

        var best = Evaluate(entry.Title, entry.Artist, entry.Album, entry.Duration);
        if (best.item != null && best.score >= ConfidentScore) return (best.item.Track, best.score);

        if (string.IsNullOrWhiteSpace(entry.Artist))
        {
            // Pas d'artiste mais un « A - B » dans le titre : on essaie les deux sens.
            var dash = SpacedDash.Match(entry.Title ?? string.Empty);
            if (dash.Success)
            {
                string a = dash.Groups["a"].Value.Trim();
                string b = dash.Groups["b"].Value.Trim();
                var r1 = Evaluate(b, a, entry.Album, entry.Duration);
                if (r1.score > best.score) best = r1;
                var r2 = Evaluate(a, b, entry.Album, entry.Duration);
                if (r2.score > best.score) best = r2;
            }
        }
        else
        {
            // Colonnes éventuellement inversées dans le fichier (Artiste / Titre).
            var inverted = Evaluate(entry.Artist, entry.Title, entry.Album, entry.Duration);
            if (inverted.score >= 0.75 && inverted.score > best.score) best = inverted;
        }

        if (best.item != null && best.score >= AcceptScore) return (best.item.Track, best.score);
        return (null, best.score);
    }

    private (Item? item, double score) Evaluate(string? title, string? artist, string? album, TimeSpan? duration)
    {
        string qFull = NormalizeText(title);
        string qCore = NormalizeText(StripNoiseAndParentheses(title ?? string.Empty));
        if (qCore.Length == 0) qCore = qFull;
        if (qFull.Length == 0) return (null, 0.0);

        string qArtist = NormalizeText(artist);
        List<string> qParts = qArtist.Length == 0 ? new List<string>() : SplitArtists(artist);
        string qAlbum = NormalizeText(album);

        var scored = new List<(Item item, double score)>();
        foreach (Item cand in GatherCandidates(qFull, qCore, qParts))
        {
            double s = ScoreCandidate(cand, qFull, qCore, qArtist, qParts, qAlbum, duration);
            if (s > 0) scored.Add((cand, s));
        }
        if (scored.Count == 0) return (null, 0.0);

        var best = scored[0];
        foreach (var x in scored) if (x.score > best.score) best = x;

        // Titre seul (sans artiste) et plusieurs artistes différents aussi plausibles : on ne peut pas être sûr.
        double final = best.score;
        if (qArtist.Length == 0 && final >= AcceptScore
            && scored.Any(x => !ReferenceEquals(x.item, best.item) && x.item.ArtistFull != best.item.ArtistFull && x.score >= best.score - 0.03))
        {
            final = Math.Min(final, 0.78);
        }
        return (best.item, final);
    }

    private IEnumerable<Item> GatherCandidates(string qFull, string qCore, List<string> qParts)
    {
        if (_items.Count <= MaxCandidates) return _items;   // petite bibliothèque : on compare tout

        var counts = new Dictionary<Item, int>();
        void Add(List<Item>? list, int weight)
        {
            if (list == null) return;
            foreach (Item it in list) counts[it] = counts.TryGetValue(it, out int c) ? c + weight : weight;
        }

        if (_titleIndex.TryGetValue(qFull, out var exact)) Add(exact, 100);
        if (qCore != qFull && _titleIndex.TryGetValue(qCore, out var exactCore)) Add(exactCore, 100);

        // Mots trop fréquents (présents dans plus de 20 % de la bibliothèque) : non discriminants, ignorés.
        int limit = Math.Max(60, _items.Count / 5);
        var lists = new List<List<Item>>();
        foreach (string w in IndexWords(qCore, qParts))
        {
            if (!_wordIndex.TryGetValue(w, out var list)) continue;
            lists.Add(list);
            if (list.Count <= limit) Add(list, 1);
        }

        // Rien de discriminant : on prend au moins le mot le plus rare.
        if (counts.Count == 0 && lists.Count > 0) Add(lists.OrderBy(l => l.Count).First(), 1);

        return counts.OrderByDescending(kv => kv.Value).Take(MaxCandidates).Select(kv => kv.Key).ToList();
    }

    private static double ScoreCandidate(Item c, string qFull, string qCore, string qArtist, List<string> qParts, string qAlbum, TimeSpan? duration)
    {
        double titleSim = TitleSimilarity(qFull, qCore, c.TitleFull, c.TitleCore);
        double score;

        if (qArtist.Length > 0 && c.ArtistFull.Length > 0)
        {
            double artistSim = ArtistSimilarity(qArtist, qParts, c.ArtistFull, c.ArtistParts);
            if (artistSim < 0.50) return 0.0;      // un autre artiste n'est jamais une correspondance, même avec le même titre
            if (titleSim < 0.60) return 0.0;
            score = 0.65 * titleSim + 0.35 * artistSim;
        }
        else
        {
            // Artiste inconnu d'un côté : on se fie au titre, avec un score plafonné (moins fiable).
            score = titleSim >= 0.60 ? 0.65 * titleSim + 0.35 * 0.70 : 0.0;

            // Pas d'artiste dans le fichier mais « Artiste Titre » dans la ligne : tous les mots doivent se retrouver.
            if (qArtist.Length == 0)
            {
                double combo = TokenDice(Tokens(qCore), c.Tokens);
                if (combo >= 0.80) score = Math.Max(score, 0.90 * combo);
            }
            if (score <= 0) return 0.0;
        }

        if (qAlbum.Length > 0 && c.Album.Length > 0 && StringSimilarity(qAlbum, c.Album) >= 0.75) score += 0.04;

        if (duration.HasValue && c.Track.Duration > TimeSpan.Zero)
        {
            double diff = Math.Abs((c.Track.Duration - duration.Value).TotalSeconds);
            if (diff <= 3.0) score += 0.06;
            else if (diff <= 8.0) score += 0.02;
            else if (diff > 60.0) score -= titleSim >= 0.97 ? 0.10 : 0.15;   // sans doute une autre version du morceau
        }
        return Math.Min(1.0, score);
    }

    // ------------------------------------------------------------------
    //  Similarités
    // ------------------------------------------------------------------

    private static double TitleSimilarity(string qFull, string qCore, string cFull, string cCore)
    {
        if (qFull == cFull) return 1.0;
        if (Compact(qFull) == Compact(cFull)) return 0.99;     // « Jay-Z » / « Jayz », « AC/DC » / « ACDC »
        if (qCore == cCore || Compact(qCore) == Compact(cCore)) return 0.97;   // même titre à la version / aux invités près
        return Math.Max(StringSimilarity(qFull, cFull), StringSimilarity(qCore, cCore));
    }

    private static double ArtistSimilarity(string qFull, List<string> qParts, string cFull, List<string> cParts)
    {
        if (qFull == cFull || Compact(qFull) == Compact(cFull)) return 1.0;
        if (qParts.Count > 0 && cParts.Count > 0 && qParts[0] == cParts[0]) return 0.95;

        foreach (string qp in qParts)
            foreach (string cp in cParts)
                if (qp.Length >= 3 && qp == cp) return 0.90;

        // « the beatles » contient « beatles » : mot entier uniquement (jamais un morceau de mot).
        if (qFull.Length >= 4 && cFull.Length >= 4 && (ContainsWholeWords(cFull, qFull) || ContainsWholeWords(qFull, cFull))) return 0.85;

        double best = Math.Max(LevenshteinSimilarity(qFull, cFull), TokenDice(Tokens(qFull), Tokens(cFull)));
        foreach (string qp in qParts)
            foreach (string cp in cParts)
                best = Math.Max(best, LevenshteinSimilarity(qp, cp) * 0.95);
        return best;
    }

    private static double StringSimilarity(string s1, string s2)
    {
        if (s1.Length == 0 || s2.Length == 0) return 0.0;
        if (s1 == s2) return 1.0;
        return Math.Max(LevenshteinSimilarity(s1, s2), Math.Max(WordContainment(s1, s2), 0.90 * TokenDice(Tokens(s1), Tokens(s2))));
    }

    private static bool ContainsWholeWords(string longer, string shorter)
        => (" " + longer + " ").Contains(" " + shorter + " ", StringComparison.Ordinal);

    /// <summary>Un titre contenu dans l'autre, mots entiers et longueurs proches (« Hey » dans « Hey Jude » ne compte pas).</summary>
    private static double WordContainment(string s1, string s2)
    {
        string shorter = s1.Length <= s2.Length ? s1 : s2;
        string longer = s1.Length <= s2.Length ? s2 : s1;
        if (shorter.Length == 0 || !ContainsWholeWords(longer, shorter)) return 0.0;
        double ratio = (double)shorter.Length / longer.Length;
        return ratio >= 0.60 ? 0.80 + 0.20 * ratio : ratio;
    }

    private static string Compact(string s) => s.Replace(" ", string.Empty);

    private static HashSet<string> Tokens(string norm)
    {
        string[] all = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var set = new HashSet<string>(all.Where(w => w.Length >= 2 && !StopWords.Contains(w)), StringComparer.Ordinal);
        if (set.Count == 0) foreach (string w in all) set.Add(w);
        return set;
    }

    private static double TokenDice(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0.0;
        int overlap = a.Count(w => b.Contains(w));
        return overlap == 0 ? 0.0 : (2.0 * overlap) / (a.Count + b.Count);
    }

    // ------------------------------------------------------------------
    //  API publique conservée (anciens noms)
    // ------------------------------------------------------------------

    public static double CalculateTitleSimilarity(string qNorm, string qBase, string cNorm, string cBase)
        => TitleSimilarity(qNorm, qBase, cNorm, cBase);

    public static double CalculateArtistSimilarity(string a1, string a2)
    {
        string n1 = NormalizeText(a1), n2 = NormalizeText(a2);
        if (n1.Length == 0 || n2.Length == 0) return 0.70;   // neutre si l'un manque
        return ArtistSimilarity(n1, SplitArtists(a1), n2, SplitArtists(a2));
    }

    public static double CalculateStringSimilarity(string s1, string s2) => StringSimilarity(s1 ?? string.Empty, s2 ?? string.Empty);

    public static double SubstringContainment(string s1, string s2) => WordContainment(s1 ?? string.Empty, s2 ?? string.Empty);

    public static double TokenSetSimilarity(string s1, string s2) => TokenDice(Tokens(NormalizeText(s1)), Tokens(NormalizeText(s2)));

    public static double LevenshteinSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return 1.0;

        int m = s1.Length, n = s2.Length;
        int maxLen = Math.Max(m, n);
        // Longueurs trop différentes : la similarité ne peut pas dépasser 0,4, inutile de calculer.
        if ((double)Math.Min(m, n) / maxLen < 0.40) return 0.0;

        if (m > n)
        {
            (s1, s2) = (s2, s1);
            (m, n) = (n, m);
        }

        int[] prev = new int[m + 1];
        int[] curr = new int[m + 1];
        for (int i = 0; i <= m; i++) prev[i] = i;

        for (int j = 1; j <= n; j++)
        {
            curr[0] = j;
            char c2 = char.ToLowerInvariant(s2[j - 1]);
            for (int i = 1; i <= m; i++)
            {
                int cost = char.ToLowerInvariant(s1[i - 1]) == c2 ? 0 : 1;
                curr[i] = Math.Min(Math.Min(curr[i - 1] + 1, prev[i] + 1), prev[i - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return Math.Max(0.0, 1.0 - (double)prev[m] / maxLen);
    }

    /// <summary>
    /// Retire d'un titre BRUT (avant normalisation !) ce qui ne décrit que la version ou les invités :
    /// « (feat. X) », « [Remastered 2011] », « - Radio Edit », « - 2011 Remaster », « feat. X ».
    /// </summary>
    public static string StripNoiseAndParentheses(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        string s = BracketNoise.Replace(title, " ");
        s = TrailingNoise.Replace(s, string.Empty);
        s = FeatTail.Replace(s, string.Empty);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Minuscules, sans accents, ponctuation → espace, apostrophes supprimées (« don't » = « dont »), « &amp; » = « and ».
    /// </summary>
    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length + 4);
        bool lastSpace = true;

        void Space()
        {
            if (!lastSpace) { sb.Append(' '); lastSpace = true; }
        }
        void Append(string s)
        {
            sb.Append(s);
            lastSpace = false;
        }

        foreach (char raw in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark) continue;

            char c = char.ToLowerInvariant(raw);
            switch (c)
            {
                case '\'': case '’': case '‘': case '`': case '´':
                    continue;
                case '&':
                    Space(); Append("and"); Space();
                    continue;
                case 'ø': Append("o"); continue;
                case 'æ': Append("ae"); continue;
                case 'œ': Append("oe"); continue;
                case 'ß': Append("ss"); continue;
                case 'ł': Append("l"); continue;
                case 'đ': Append("d"); continue;
                case 'þ': Append("th"); continue;
            }

            if (char.IsLetterOrDigit(c)) Append(c.ToString());
            else Space();
        }
        return sb.ToString().Trim();
    }

    /// <summary>Découpe un artiste BRUT : « A feat. B », « A &amp; B », « A, B », « A; B », « A x B », « A / B ».</summary>
    public static List<string> SplitArtists(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return new List<string>();
        var parts = ArtistSplit.Split(artist)
                               .Select(NormalizeText)
                               .Where(p => p.Length > 0)
                               .ToList();
        if (parts.Count == 0)
        {
            string whole = NormalizeText(artist);
            if (whole.Length > 0) parts.Add(whole);
        }
        return parts;
    }

    public static List<string> GetMeaningfulWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return NormalizeText(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3).ToList();
    }
}

/// <summary>Résultat de l'appariement d'une ligne du fichier avec la bibliothèque.</summary>
public sealed class PlaylistImportMatch
{
    public PlaylistImportEntry Entry { get; init; } = new();
    public Track? Track { get; init; }
    public double Score { get; init; }
    /// <summary>Correspondance trouvée mais pas assez sûre pour être ajoutée sans relecture.</summary>
    public bool NeedsReview => Track != null && Score < FuzzyTrackMatcher.ConfidentScore;
}

public sealed class PlaylistImportAnalysis
{
    public List<PlaylistImportMatch> Matches { get; } = new();
    public string? CoverPath { get; set; }
}

/// <summary>
/// Service d'importation de playlists à partir de fichiers d'export (Spotify CSV, Soundiiz, TuneMyMusic, TXT, etc.).
/// </summary>
public static class PlaylistImportService
{
    /// <summary>
    /// Lit le fichier et apparie chaque ligne à la bibliothèque (avec le score), sans rien ajouter nulle part.
    /// Lève une exception si le fichier est illisible.
    /// </summary>
    public static Task<PlaylistImportAnalysis> AnalyzeExportFileAsync(string filePath, IEnumerable<Track> library)
    {
        return Task.Run(() =>
        {
            var analysis = new PlaylistImportAnalysis();
            var entries = PlaylistExportParser.ParseFile(filePath);
            if (entries.Count == 0) return analysis;

            var matcher = new FuzzyTrackMatcher(library);
            foreach (var entry in entries)
            {
                var (track, score) = matcher.Match(entry);
                analysis.Matches.Add(new PlaylistImportMatch { Entry = entry, Track = track, Score = score });
            }

            // Pochette associée dans le même dossier (même nom que le fichier)
            try
            {
                string baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(filePath);
                foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
                {
                    string candidate = Path.Combine(baseDir, name + ext);
                    if (File.Exists(candidate))
                    {
                        analysis.CoverPath = candidate;
                        break;
                    }
                }
            }
            catch { }

            return analysis;
        });
    }

    /// <summary>
    /// Import non interactif (utilisé par PlaylistIO) : toutes les correspondances acceptées, sans doublon, dans l'ordre du fichier.
    /// </summary>
    public static async Task<(List<string> resolvedPaths, List<string> missing, string? coverPath)> ImportExportFileAsync(
        string filePath, IEnumerable<Track> library)
    {
        var analysis = await AnalyzeExportFileAsync(filePath, library);

        var resolvedPaths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var m in analysis.Matches)
        {
            if (m.Track != null && !string.IsNullOrWhiteSpace(m.Track.FilePath))
            {
                if (seen.Add(m.Track.FilePath)) resolvedPaths.Add(m.Track.FilePath);
            }
            else
            {
                missing.Add(m.Entry.ToString());
            }
        }
        return (resolvedPaths, missing, analysis.CoverPath);
    }
}
