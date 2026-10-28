// ============================================================
//  MainWindow.YearInReview.cs  —  Resona
//  Affichage du "Year in review" (ecran plein cadre a la place du contenu courant, restaure a la fermeture)
//  et proposition automatique au lancement : du 20 decembre au 31 janvier, une seule fois par annee.
// ============================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;
using Resona.Services;
using Resona.Views;

namespace Resona;

public sealed partial class MainWindow
{
    private static string YL(string fr, string en) => Strings.Current.IsFr ? fr : en;

    private XamlRoot? DialogRoot => ((FrameworkElement)((Window)this).Content).XamlRoot;

    /// <summary>
    /// Vrai tant que l'ecran du bilan est affiche. Les raccourcis clavier globaux (Espace = lecture/pause, gauche/droite = titre
    /// precedent/suivant) sont alors laisses a l'ecran du bilan, qui s'en sert pour changer de page.
    /// </summary>
    private bool _yearInReviewOpen;

    /// <summary>
    /// Petite boite d'information avec un seul bouton OK centre. Dessinee a la main (carte centree sur fond assombri) plutot qu'avec
    /// ContentDialog, dont la zone des boutons reservait toujours un grand vide sous un bouton personnalise.
    /// </summary>
    private Task ShowCenteredInfoDialogAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();
        bool dark = RootGrid.ActualTheme != ElementTheme.Light;

        var overlay = new Grid { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(150, 0, 0, 0)), Opacity = 0 };
        Grid.SetRowSpan(overlay, 2);
        Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(overlay, 2000);

        var ok = new Button
        {
            Content = "OK",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0)
        };
        try { ok.Style = (Style)Application.Current.Resources["AccentButtonStyle"]; } catch { /* style par defaut */ }

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(ok);

        var card = new Border
        {
            MinWidth = 320,
            MaxWidth = 460,
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 44, 44, 44) : Windows.UI.Color.FromArgb(255, 243, 243, 243)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 70, 70, 70) : Windows.UI.Color.FromArgb(255, 210, 210, 210)),
            Child = content
        };
        overlay.Children.Add(card);

        void Close()
        {
            RootGrid.Children.Remove(overlay);
            _yearInReviewOpen = false;   // les raccourcis globaux redeviennent actifs
            tcs.TrySetResult();
        }
        ok.Click += (_, _) => Close();
        overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape) { Close(); e.Handled = true; }
        };

        // Pendant l'affichage, Espace / fleches ne doivent pas piloter la lecture.
        _yearInReviewOpen = true;
        RootGrid.Children.Add(overlay);
        ok.Loaded += (_, _) => ok.Focus(FocusState.Programmatic);

        var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(180)
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, overlay);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        sb.Children.Add(fade);
        sb.Begin();

        return tcs.Task;
    }

    /// <summary>Ouvre le bilan de l'annee donnee. debugData (build Debug uniquement) : utilise de fausses ecoutes generees.</summary>
    public async void ShowYearInReview(int year, bool debugData = false)
    {
        try
        {
            var library = _library ?? new List<Track>();
            List<PlayEvent> events = App.PlayHistory.Snapshot();
            DateTime trackingStart = App.PlayHistory.TrackingStartUtc;
#if DEBUG
            if (debugData)
            {
                events = YearInReviewService.GenerateDebugEvents(year, library);
                trackingStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
            }
#endif
            var data = YearInReviewService.Compute(year, events, trackingStart);
            if (!data.HasEnoughData)
            {
                await ShowCenteredInfoDialogAsync(
                    YL("Pas encore assez de données", "Not enough data yet"),
                    YL("Le bilan se construit au fil de vos écoutes. Écoutez quelques titres, puis revenez ici !",
                       "The review builds up as you listen. Play a few tracks, then come back here!"));
                return;
            }

            var covers = new Dictionary<string, string?>();
            foreach (var t in library) covers[t.Id] = t.CoverArtPath;

            // Plein ecran : le bilan est pose PAR-DESSUS toute la fenetre (volet de navigation, barre de lecture, tout), pas seulement
            // a la place du contenu de la page. A la fermeture, on le retire et l'application est exactement comme avant.
            bool closed = false;
            YearInReviewPage? page = null;
            page = new YearInReviewPage(data, covers, () =>
            {
                if (closed) return;
                closed = true;
                _yearInReviewOpen = false;
                if (page != null) RootGrid.Children.Remove(page);
            });
            // Retire par un autre moyen : les raccourcis globaux redeviennent actifs.
            page.Unloaded += (_, _) => _yearInReviewOpen = false;

            Grid.SetRowSpan(page, 2);
            Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(page, 1000);
            _yearInReviewOpen = true;
            RootGrid.Children.Add(page);
        }
        catch
        {
            _yearInReviewOpen = false;
            // Un probleme d'affichage du bilan ne doit jamais faire planter l'app.
        }
    }

    /// <summary>Attend que la fenetre soit chargee (une boite de dialogue a besoin du XamlRoot), 10 s au maximum.</summary>
    private async Task WaitUntilWindowReadyAsync()
    {
        var root = (FrameworkElement)((Window)this).Content;
        if (root.XamlRoot != null && root.IsLoaded) return;

        var tcs = new TaskCompletionSource();
        RoutedEventHandler? handler = null;
        handler = (_, _) => { root.Loaded -= handler; tcs.TrySetResult(); };
        root.Loaded += handler;
        await Task.WhenAny(tcs.Task, Task.Delay(10000));
    }

    /// <summary>
    /// La boite de dialogue qui propose le bilan. Renvoie le bouton choisi, ou null si elle n'a pas pu s'afficher
    /// (une autre boite de dialogue de lancement est ouverte : on reessaie chaque seconde pendant 10 s).
    /// </summary>
    private async Task<ContentDialogResult?> ShowYearInReviewPromptDialogAsync(int year, bool debug)
    {
        await WaitUntilWindowReadyAsync();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = DialogRoot,
                Title = (debug ? "[DEBUG] " : "") + string.Format(YL("Votre bilan {0} est prêt 🎉", "Your {0} in review is ready 🎉"), year),
                Content = YL("Découvrez vos titres et artistes les plus écoutés, votre temps d'écoute et plus encore, puis exportez le tout en image.\n\nVous pourrez le retrouver à tout moment dans la page Statistiques.",
                             "See your most played tracks and artists, your listening time and more, then export it all as an image.\n\nYou can find it again any time in the Statistics page."),
                PrimaryButtonText = YL("Voir mon bilan", "See my review"),
                SecondaryButtonText = YL("Ne plus me le proposer", "Don't ask again"),
                CloseButtonText = YL("Plus tard", "Later"),
                DefaultButton = ContentDialogButton.Primary
            };
            try
            {
                return await dialog.ShowAsync();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                await Task.Delay(1000);   // une autre boite de dialogue est ouverte
            }
        }
        return null;
    }

    /// <summary>
    /// Proposition automatique (appelee peu apres le lancement) : du 20 decembre au 31 janvier, si le bilan de l'annee a assez de donnees
    /// et n'a pas deja ete propose. Le bilan reste toujours accessible depuis la page Statistiques.
    /// En build Debug : la boite de dialogue s'affiche a chaque lancement (sans condition de date ni de donnees, sans rien memoriser),
    /// et "Voir mon bilan" ouvre le bilan avec de fausses donnees.
    /// </summary>
    public async void TryShowYearInReviewPrompt()
    {
        int previousPrompted = App.Settings.Current.YearInReviewPromptedYear;
        try
        {
            if (!((Window)this).AppWindow.IsVisible) return;   // demarrage en arriere-plan (barre des taches) : pas de fenetre a l'ecran

#if DEBUG
            int debugYear = DateTime.Now.Year;
            var debugResult = await ShowYearInReviewPromptDialogAsync(debugYear, debug: true);
            if (debugResult == ContentDialogResult.Primary) ShowYearInReview(debugYear, debugData: true);
            return;
#else
            // Migration : les anciens fichiers settings.json ont YearInReviewPromptDisabled=true (bool permanent).
            // On le convertit en annee courante pour que le prompt revienne l'annee d'apres.
            if (App.Settings.Current.YearInReviewPromptDisabled)
            {
                App.Settings.Current.YearInReviewPromptDismissedYear = DateTime.Now.Year;
                App.Settings.Current.YearInReviewPromptDisabled = false;
                _ = App.Settings.SaveAsync();
            }

            int? year = YearInReviewService.GetPromptYear(DateTime.Now);
            if (year == null || previousPrompted >= year.Value) return;
            if (App.Settings.Current.YearInReviewPromptDismissedYear >= year.Value) return;

            var data = YearInReviewService.Compute(year.Value, App.PlayHistory.Snapshot(), App.PlayHistory.TrackingStartUtc);
            if (!data.HasEnoughData) return;

            App.Settings.Current.YearInReviewPromptedYear = year.Value;
            _ = App.Settings.SaveAsync();

            var result = await ShowYearInReviewPromptDialogAsync(year.Value, debug: false);
            if (result == null)
            {
                // Impossible d'afficher la boite (une autre est restee ouverte) : on repropose au prochain lancement.
                App.Settings.Current.YearInReviewPromptedYear = previousPrompted;
                _ = App.Settings.SaveAsync();
                return;
            }
            if (result == ContentDialogResult.Primary) ShowYearInReview(year.Value);
            else if (result == ContentDialogResult.Secondary)
            {
                App.Settings.Current.YearInReviewPromptDismissedYear = year.Value;
                _ = App.Settings.SaveAsync();
            }
#endif
        }
        catch
        {
            // Une autre boite de dialogue est peut-etre deja ouverte : on repropose au prochain lancement.
            App.Settings.Current.YearInReviewPromptedYear = previousPrompted;
        }
    }
}
