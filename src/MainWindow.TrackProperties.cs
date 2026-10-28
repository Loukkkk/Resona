using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Resona.Helpers;
using Resona.Models;

namespace Resona;

public sealed partial class MainWindow
{
	private sealed record PropRow(string Label, string Value);

	/// <summary>Fenêtre « Propriétés » d'un son : infos techniques du fichier audio.</summary>
	public async Task ShowTrackPropertiesAsync(Track track)
	{
		if (track == null) return;
		bool fr = Strings.Current.IsFr;
		string path = track.FilePath ?? string.Empty;

		var sections = await Task.Run(() => CollectTrackProperties(track, path, fr));

		var root = new StackPanel { Spacing = 4, Width = 640 };

		// Emplacement + bouton dossier
		var pathBox = new TextBlock
		{
			Text = path,
			TextWrapping = TextWrapping.Wrap,
			IsTextSelectionEnabled = true,
			Opacity = 0.85,
			FontSize = 12
		};
		var openBtn = new Button
		{
			Content = fr ? "Ouvrir le dossier" : "Open containing folder",
			Margin = new Thickness(0, 6, 0, 8),
			IsEnabled = File.Exists(path)
		};
		openBtn.Click += (_, _) =>
		{
			try
			{
				Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Path.GetFullPath(path)}\"") { UseShellExecute = true });
			}
			catch { }
		};
		root.Children.Add(new TextBlock { Text = fr ? "Emplacement" : "Location", FontWeight = FontWeights.SemiBold });
		root.Children.Add(pathBox);
		root.Children.Add(openBtn);

		foreach (var (title, rows) in sections)
		{
			if (rows.Count == 0) continue;
			root.Children.Add(new TextBlock
			{
				Text = title,
				FontWeight = FontWeights.SemiBold,
				FontSize = 15,
				Margin = new Thickness(0, 10, 0, 2)
			});
			var grid = new Grid { ColumnSpacing = 12, RowSpacing = 3 };
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			for (int i = 0; i < rows.Count; i++)
			{
				grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				var l = new TextBlock { Text = rows[i].Label, Opacity = 0.6, TextWrapping = TextWrapping.Wrap };
				var v = new TextBlock { Text = rows[i].Value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
				Grid.SetRow(l, i);
				Grid.SetRow(v, i);
				Grid.SetColumn(v, 1);
				grid.Children.Add(l);
				grid.Children.Add(v);
			}
			root.Children.Add(grid);
		}

		var scroll = new ScrollViewer
		{
			Content = root,
			MaxHeight = 520,
			Width = 660,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Padding = new Thickness(0, 0, 14, 0)
		};

		var dialog = new ContentDialog
		{
			Title = fr ? "Propriétés" : "Properties",
			Content = scroll,
			CloseButtonText = fr ? "Fermer" : "Close",
			DefaultButton = ContentDialogButton.Close,
			CenterCloseButton = true,
			XamlRoot = ((UIElement)ContentFrame).XamlRoot
		};
		dialog.Resources["ContentDialogMinWidth"] = 720.0;
		dialog.Resources["ContentDialogMaxWidth"] = 720.0;
		try { await dialog.ShowAsync(); } catch { }
	}

	private static List<(string, List<PropRow>)> CollectTrackProperties(Track track, string path, bool fr)
	{
		string T(string f, string e) => fr ? f : e;
		var ci = CultureInfo.CurrentCulture;
		var file = new List<PropRow>();
		var audio = new List<PropRow>();
		var tags = new List<PropRow>();
		var lib = new List<PropRow>();

		void Add(List<PropRow> l, string label, string? value)
		{
			if (!string.IsNullOrWhiteSpace(value)) l.Add(new PropRow(label, value!));
		}

		long size = 0;
		bool exists = false;
		try
		{
			var fi = new FileInfo(path);
			exists = fi.Exists;
			if (exists)
			{
				size = fi.Length;
				Add(file, T("Nom du fichier", "File name"), fi.Name);
				Add(file, T("Dossier", "Folder"), fi.DirectoryName);
				Add(file, T("Extension", "Extension"), fi.Extension);
				Add(file, T("Taille", "Size"), $"{FormatBytes(size)} ({size.ToString("N0", ci)} {T("octets", "bytes")})");
				Add(file, T("Créé le", "Created"), fi.CreationTime.ToString("g", ci));
				Add(file, T("Modifié le", "Modified"), fi.LastWriteTime.ToString("g", ci));
				Add(file, T("Dernier accès", "Last accessed"), fi.LastAccessTime.ToString("g", ci));
				Add(file, T("Attributs", "Attributes"), fi.Attributes.ToString());
				if (fi.IsReadOnly) Add(file, T("Lecture seule", "Read-only"), T("Oui", "Yes"));
				try
				{
					var root = Path.GetPathRoot(fi.FullName);
					if (!string.IsNullOrEmpty(root))
					{
						var di = new DriveInfo(root);
						Add(file, T("Lecteur", "Drive"), $"{di.Name} ({di.DriveType}{(di.IsReady ? ", " + di.DriveFormat : "")})");
					}
				}
				catch { }
				try
				{
					using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					var head = new byte[16];
					int n = fs.Read(head, 0, head.Length);
					Add(file, T("Signature (octets)", "Header bytes"), BitConverter.ToString(head, 0, n).Replace('-', ' '));
					Add(file, T("Conteneur détecté", "Detected container"), DetectContainer(head, n));
				}
				catch { }
			}
			else
			{
				Add(file, T("État", "Status"), T("Fichier introuvable sur le disque", "File not found on disk"));
			}
		}
		catch { }

		if (exists)
		{
			try
			{
				using var tf = TagLib.File.Create(path);
				var p = tf.Properties;
				Add(audio, T("Type MIME", "MIME type"), tf.MimeType);
				Add(audio, T("Format TagLib", "TagLib format"), tf.GetType().Name);
				try
				{
					var codecs = p.Codecs?.Where(c => c != null).Select(c => c.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
					if (codecs != null && codecs.Count > 0) Add(audio, "Codec", string.Join(" | ", codecs));
				}
				catch { }
				Add(audio, T("Types de média", "Media types"), p.MediaTypes.ToString());
				if (p.Duration.TotalSeconds > 0)
					Add(audio, T("Durée", "Duration"), $"{(p.Duration.TotalHours >= 1 ? p.Duration.ToString(@"h\:mm\:ss\.fff") : p.Duration.ToString(@"m\:ss\.fff"))} ({p.Duration.TotalSeconds.ToString("F2", ci)} s)");
				if (p.AudioBitrate > 0) Add(audio, T("Débit", "Bitrate"), $"{p.AudioBitrate} kb/s");
				if (p.AudioSampleRate > 0) Add(audio, T("Fréquence d'échantillonnage", "Sample rate"), $"{p.AudioSampleRate.ToString("N0", ci)} Hz");
				if (p.BitsPerSample > 0) Add(audio, T("Profondeur de bits", "Bit depth"), $"{p.BitsPerSample} bits");
				if (p.AudioChannels > 0)
					Add(audio, T("Canaux", "Channels"), p.AudioChannels switch { 1 => "1 (mono)", 2 => "2 (stéréo)", _ => p.AudioChannels.ToString() });
				if (p.Duration.TotalSeconds > 0 && size > 0)
					Add(audio, T("Débit réel moyen", "Actual average bitrate"), $"{(size * 8.0 / p.Duration.TotalSeconds / 1000.0).ToString("F0", ci)} kb/s");
				if (p.BitsPerSample > 0 && p.AudioSampleRate > 0 && p.AudioChannels > 0)
				{
					long pcm = (long)p.AudioSampleRate * p.BitsPerSample * p.AudioChannels / 1000;
					Add(audio, T("Débit PCM équivalent", "Equivalent PCM bitrate"), $"{pcm.ToString("N0", ci)} kb/s");
				}
				bool lossless = p.Codecs != null && p.Codecs.Any(c => c != null && (c.Description ?? "").Contains("Lossless", StringComparison.OrdinalIgnoreCase));
				string ext = Path.GetExtension(path).ToLowerInvariant();
				if (lossless || ext is ".flac" or ".wav" or ".aiff" or ".aif" or ".alac")
					Add(audio, T("Compression", "Compression"), T("Sans perte (lossless)", "Lossless"));
				else if (ext is ".mp3" or ".ogg" or ".opus" or ".m4a" or ".aac" or ".wma")
					Add(audio, T("Compression", "Compression"), T("Avec perte (lossy)", "Lossy"));

				var tag = tf.Tag;
				Add(tags, T("Types de tags", "Tag types"), tf.TagTypes.ToString());
				Add(tags, T("Titre", "Title"), tag.Title);
				Add(tags, T("Artiste(s)", "Artist(s)"), tag.JoinedPerformers);
				Add(tags, T("Artiste de l'album", "Album artist"), tag.JoinedAlbumArtists);
				Add(tags, "Album", tag.Album);
				Add(tags, T("Genre(s)", "Genre(s)"), tag.JoinedGenres);
				if (tag.Year > 0) Add(tags, T("Année", "Year"), tag.Year.ToString());
				if (tag.Track > 0) Add(tags, T("Piste", "Track"), tag.TrackCount > 0 ? $"{tag.Track}/{tag.TrackCount}" : tag.Track.ToString());
				if (tag.Disc > 0) Add(tags, T("Disque", "Disc"), tag.DiscCount > 0 ? $"{tag.Disc}/{tag.DiscCount}" : tag.Disc.ToString());
				if (tag.BeatsPerMinute > 0) Add(tags, "BPM", tag.BeatsPerMinute.ToString());
				Add(tags, T("Compositeur(s)", "Composer(s)"), tag.JoinedComposers);
				Add(tags, "Copyright", tag.Copyright);
				Add(tags, T("Commentaire", "Comment"), tag.Comment);
				Add(tags, T("Pochette intégrée", "Embedded cover"), tag.Pictures is { Length: > 0 } pics
					? string.Join(", ", pics.Select(x => $"{x.MimeType} ({FormatBytes(x.Data?.Count ?? 0)})"))
					: T("Aucune", "None"));
				Add(tags, T("Paroles intégrées", "Embedded lyrics"), string.IsNullOrWhiteSpace(tag.Lyrics) ? T("Non", "No") : $"{tag.Lyrics.Length.ToString("N0", ci)} {T("caractères", "characters")}");
				if (!double.IsNaN(tag.ReplayGainTrackGain)) Add(tags, "ReplayGain (" + T("piste", "track") + ")", $"{tag.ReplayGainTrackGain:F2} dB");
				if (!double.IsNaN(tag.ReplayGainAlbumGain)) Add(tags, "ReplayGain (album)", $"{tag.ReplayGainAlbumGain:F2} dB");
				Add(tags, "MusicBrainz Track Id", tag.MusicBrainzTrackId);
				Add(tags, "MusicBrainz Release Id", tag.MusicBrainzReleaseId);
			}
			catch (Exception ex)
			{
				Add(audio, T("Lecture des métadonnées", "Metadata read"), T("Impossible : ", "Failed: ") + ex.Message);
			}
		}

		Add(lib, T("Identifiant", "Track Id"), track.Id);
		Add(lib, T("Ajouté à la bibliothèque", "Added to library"), track.DateAdded.ToLocalTime().ToString("g", ci));
		if (track.LastModified != default) Add(lib, T("Dernière modification (cache)", "Last modified (cache)"), track.LastModified.ToString("g", ci));
		Add(lib, T("Gain de normalisation", "Normalization gain"), track.NormalizationGainDb != 0 ? $"{track.NormalizationGainDb:+0.00;-0.00} dB" : null);
		Add(lib, T("Analysé", "Analyzed"), track.IsAnalyzed ? T("Oui", "Yes") : T("Non", "No"));
		Add(lib, T("Favori", "Favorite"), track.IsFavorite ? T("Oui", "Yes") : T("Non", "No"));
		Add(lib, T("Source externe", "External source"), track.IsExternal ? T("Oui", "Yes") : null);

		return new List<(string, List<PropRow>)>
		{
			(T("Fichier", "File"), file),
			(T("Audio", "Audio"), audio),
			(T("Métadonnées", "Metadata"), tags),
			(T("Bibliothèque", "Library"), lib),
		};
	}

	private static string FormatBytes(long b)
	{
		string[] u = { "o", "Ko", "Mo", "Go" };
		double v = b; int i = 0;
		while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
		return $"{v.ToString(i == 0 ? "F0" : "F2", CultureInfo.CurrentCulture)} {u[i]}";
	}

	private static string DetectContainer(byte[] h, int n)
	{
		if (n >= 4 && h[0] == 'f' && h[1] == 'L' && h[2] == 'a' && h[3] == 'C') return "FLAC";
		if (n >= 4 && h[0] == 'O' && h[1] == 'g' && h[2] == 'g' && h[3] == 'S') return "Ogg";
		if (n >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'A' && h[10] == 'V' && h[11] == 'E') return "RIFF / WAVE";
		if (n >= 12 && h[0] == 'F' && h[1] == 'O' && h[2] == 'R' && h[3] == 'M') return "AIFF";
		if (n >= 8 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p') return "MPEG-4 (ISO BMFF)";
		if (n >= 3 && h[0] == 'I' && h[1] == 'D' && h[2] == '3') return "MP3 (ID3v2)";
		if (n >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0) return (h[1] & 0x06) == 0x00 ? "AAC (ADTS)" : "MPEG Audio (MP3)";
		if (n >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3) return "Matroska / WebM";
		if (n >= 4 && h[0] == 0x30 && h[1] == 0x26 && h[2] == 0xB2 && h[3] == 0x75) return "ASF / WMA";
		return "?";
	}
}
