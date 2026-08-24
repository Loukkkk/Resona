using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using WinRT;

namespace Resona.Views;

public sealed partial class LyricsPage : Page
{

	public LyricsPage()
	{
		InitializeComponent();
	}

	public void ShowLyrics(string? lyrics)
	{
		LyricsText.Text = (string.IsNullOrWhiteSpace(lyrics) ? (Models.Strings.Current.IsFr ? "Aucune parole trouvée pour ce morceau." : "No lyrics found for this track.") : lyrics);
	}
}
