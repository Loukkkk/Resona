using Microsoft.UI.Xaml.Controls;
using Resona.Models;

namespace Resona.Views;

public sealed partial class NowPlayingPage : Page
{
    private string? _currentTrackId;

    public NowPlayingPage()
    {
        InitializeComponent();
    }

    private void RootContainer_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindowInstance?.SetNowPlayingMode(true, _currentTrackId);
    }

    private void RootContainer_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindowInstance?.SetNowPlayingMode(false, null);
    }

    public void SetNowPlayingId(string? id, string? filePath)
    {
        _currentTrackId = id;
    }

    public void UpdateTrackInfo(Track track)
    {
        _currentTrackId = track.Id;
        App.MainWindowInstance?.UpdateNowPlayingBackground(track.CoverArtPath);
    }
}
