using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Resona.Services;

namespace Resona.Views;

/// <summary>
/// Sections pliables / depliables de la page Parametres.
/// Chaque section (titre + cartes) devient un bloc dont on peut masquer le contenu en cliquant sur son titre.
/// L'etat (plie / deplie) est memorise dans les reglages.
/// </summary>
public sealed partial class SettingsPage
{
    // Cles stables, dans l'ordre d'affichage des sections (une cle par section).
    private static readonly string[] SectionKeys =
    {
        "language", "player_buttons", "features", "system", "library", "categories",
        "audio", "playlists", "download", "assistant", "appearance", "cache", "updates", "logs"
    };

    private static bool IsSection(UIElement e) =>
        e is StackPanel p && p.Children.Count >= 2 && p.Children[0] is TextBlock;

    private const double SettingsContentWidth = 720;

    private void UpdateSettingsRootWidth(double pageWidth)
    {
        if (pageWidth <= 0) return;
        // Marges horizontales de la page : 24 (gauche) + 32 (droite) + 8 (marge du panneau).
        double available = pageWidth - 24 - 32 - 8;
        SettingsRoot.Width = Math.Max(0, Math.Min(SettingsContentWidth, available));
    }

    private void SetupCollapsibleSections()
    {
        try
        {
            var sections = new List<StackPanel>();

            foreach (var child in SettingsRoot.Children.ToList())
            {
                if (child is not StackPanel sp || !IsSection(sp)) continue;
                sections.Add(sp);

                // Les sous-sections (cache, mises a jour, journal) sont imbriquees dans « Apparence » :
                // on les sort pour qu'elles se plient chacune separement.
                int at = SettingsRoot.Children.IndexOf(sp);
                foreach (var nested in sp.Children.Skip(2).Where(IsSection).Cast<StackPanel>().ToList())
                {
                    sp.Children.Remove(nested);
                    nested.Margin = new Thickness(0);
                    nested.Spacing = 12;
                    SettingsRoot.Children.Insert(++at, nested);
                    sections.Add(nested);
                }
            }

            var collapsedKeys = App.Settings.Current.CollapsedSettingsSections ??= new List<string>();
            for (int i = 0; i < sections.Count; i++)
            {
                string key = i < SectionKeys.Length ? SectionKeys[i] : "section_" + i;
                MakeCollapsible(sections[i], key, collapsedKeys);
            }

            // Largeur fixe : sans ca, le panneau s'adapte a la section visible la plus large et la largeur varie quand on en replie.
            SizeChanged += (s, e) => UpdateSettingsRootWidth(e.NewSize.Width);
            UpdateSettingsRootWidth(ActualWidth);
        }
        catch (Exception ex)
        {
            LogService.Write("Settings", ex, "Sections pliables");
        }
    }

    private void MakeCollapsible(StackPanel section, string key, List<string> collapsedKeys)
    {
        var header = (TextBlock)section.Children[0];
        var items = section.Children.Skip(1).ToList();
        section.Children.Clear();
        section.Spacing = 0;

        // ---- En-tete cliquable : titre + chevron ----
        var chevron = new FontIcon
        {
            Glyph = "\uE70D", // chevron vers le bas ; tourne de 180 degres quand la section est ouverte
            FontSize = 12,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center
        };
        var headerRow = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 12, 6),
            Margin = new Thickness(-8, 0, -8, 0)
        };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.VerticalAlignment = VerticalAlignment.Center;
        headerRow.Children.Add(header);
        Grid.SetColumn(chevron, 1);
        headerRow.Children.Add(chevron);

        // ---- Corps : les cartes d'origine, dans un conteneur dont on anime la hauteur ----
        var body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(0, 12, 0, 0),
            RenderTransform = new TranslateTransform()
        };
        foreach (var item in items) body.Children.Add(item);

        var host = new Grid();
        host.Children.Add(body);

        section.Children.Add(headerRow);
        section.Children.Add(host);

        // La rotation du chevron est pilotee par la couche de composition (Visual) et non par un Storyboard XAML :
        // un Storyboard remet la valeur d'origine a son arret, ce qui faisait « flicker » la fleche en fin d'animation.
        var chevronVisual = ElementCompositionPreview.GetElementVisual(chevron);
        chevron.SizeChanged += (s, e) =>
            chevronVisual.CenterPoint = new Vector3((float)(e.NewSize.Width / 2), (float)(e.NewSize.Height / 2), 0f);
        var slide = (TranslateTransform)body.RenderTransform;

        bool expanded = !collapsedKeys.Contains(key);
        bool animating = false;
        Storyboard? running = null;

        if (expanded) chevronVisual.RotationAngleInDegrees = 180f;
        else host.Visibility = Visibility.Collapsed;

        // Pendant l'animation, le contenu est rogne a la hauteur courante (sinon il deborde sur la section suivante).
        host.SizeChanged += (s, e) =>
        {
            if (animating)
                host.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        };

        // ---- Survol ----
        Brush hoverBrush =
            (Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out var res) ? res as Brush : null)
            ?? new SolidColorBrush(Windows.UI.Color.FromArgb(24, 128, 128, 128));
        headerRow.PointerEntered += (s, e) =>
        {
            headerRow.Background = hoverBrush;
            header.Opacity = 1;
            chevron.Opacity = 1;
        };
        headerRow.PointerExited += (s, e) =>
        {
            headerRow.Background = new SolidColorBrush(Colors.Transparent);
            header.Opacity = 0.9;
            chevron.Opacity = 0.6;
        };

        // ---- Animation ----
        void Animate(bool expand)
        {
            bool wasVisible = host.Visibility == Visibility.Visible;
            double currentHeight = wasVisible ? host.ActualHeight : 0;
            double currentOpacity = wasVisible ? host.Opacity : 0;

            if (running != null)
            {
                running.Stop();
                running = null;
            }

            double targetHeight = 0;
            if (expand)
            {
                host.Visibility = Visibility.Visible;
                host.Height = double.NaN;
                double width = section.ActualWidth > 0 ? section.ActualWidth : 720;
                host.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
                targetHeight = host.DesiredSize.Height;
            }

            animating = true;
            host.Height = currentHeight;
            host.Opacity = currentOpacity;

            var ease = new CubicEase { EasingMode = expand ? EasingMode.EaseOut : EasingMode.EaseInOut };
            var sb = new Storyboard();

            void Add(DependencyObject target, string path, double from, double to, int ms, bool dependent = false)
            {
                var a = new DoubleAnimation
                {
                    From = from,
                    To = to,
                    Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                    EasingFunction = ease,
                    EnableDependentAnimation = true // animation pilotee par le thread UI : pas de retour parasite a l'ancienne valeur a la fin
                };
                Storyboard.SetTarget(a, target);
                Storyboard.SetTargetProperty(a, path);
                sb.Children.Add(a);
            }

            Add(host, "Height", currentHeight, targetHeight, expand ? 340 : 260, dependent: true);
            Add(host, "Opacity", currentOpacity, expand ? 1 : 0, expand ? 300 : 180);
            Add(body, "(UIElement.RenderTransform).(TranslateTransform.Y)", expand ? -12 : 0, expand ? 0 : -8, expand ? 340 : 220);
            // Rotation du chevron : animation de composition. Elle part de l'angle courant (pas de saut si on reclique
            // en plein milieu) et reste sur sa valeur finale a la fin, sans retour a l'ancienne valeur.
            var compositor = chevronVisual.Compositor;
            var rotation = compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(1f, expand ? 180f : 0f, compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            rotation.Duration = TimeSpan.FromMilliseconds(280);
            chevronVisual.StartAnimation("RotationAngleInDegrees", rotation);

            sb.Completed += (s, e) =>
            {
                if (!ReferenceEquals(running, sb)) return;
                running = null;
                animating = false;

                // On ecrit les valeurs finales AVANT sb.Stop() : Stop() rend la main a la valeur locale.
                host.Height = double.NaN;
                host.Opacity = 1;
                slide.Y = 0;
                sb.Stop();

                host.Clip = null;
                if (!expand) host.Visibility = Visibility.Collapsed;
            };

            running = sb;
            sb.Begin();
        }

        headerRow.Tapped += (s, e) =>
        {
            expanded = !expanded;
            if (expanded) collapsedKeys.Remove(key);
            else if (!collapsedKeys.Contains(key)) collapsedKeys.Add(key);
            _ = App.Settings.SaveAsync();
            Animate(expanded);
        };
    }
}
