using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace Resona;

/// <summary>
/// Transition de changement de son en mode grosse cover :
///  - l'ancienne cover part (decalage dans le sens oppose a l'arrivee, leger retrecissement, fondu) pendant que la nouvelle arrive ;
///  - l'ancien fond flou (base sur la couleur de l'ancienne cover) reste dessous et laisse la place, en fondu enchaine,
///    au nouveau fond flou (base sur la couleur de la nouvelle cover).
/// Les images "precedentes" sont memorisees par UpdateNowPlayingBackground juste avant d'etre remplacees.
/// </summary>
public sealed partial class MainWindow
{
    private ImageSource? _npPrevCoverSource;
    private ImageSource? _npPrevBlurSource;
    private long _npPrevStashTick;

    // Vrai quand le bouton "precedent" lance PlayTrack en lecture normale (PlayTrack ne recoit alors pas isGoingBack) :
    // sert uniquement a inverser le sens des animations de la grosse cover.
    private bool _prevNavPending;

    private Storyboard? _npOutgoingCoverStoryboard;
    private Storyboard? _npOutgoingBlurStoryboard;

    // Appele par UpdateNowPlayingBackground juste avant de remplacer la cover et le flou.
    private void StashPreviousNowPlayingImages()
    {
        _npPrevCoverSource = NowPlayingCenterCoverImage.Source;
        _npPrevBlurSource = NowPlayingBlurredBackground.Source;
        _npPrevStashTick = Environment.TickCount64;
    }

    // Appele au debut de AnimateTrackChange (changement de son alors que la grosse cover est deja affichee).
    private void PlayTrackChangeTransition(bool isGoingBack)
    {
        ImageSource? prevCover = _npPrevCoverSource;
        ImageSource? prevBlur = _npPrevBlurSource;
        bool fresh = Environment.TickCount64 - _npPrevStashTick < 1500;
        _npPrevCoverSource = null;
        _npPrevBlurSource = null;

        if (!fresh || !_isNowPlayingModeActive) return;

        try
        {
            // 1. Ancienne cover : part dans le sens oppose a l'arrivee de la nouvelle (qui arrive de +100 ou -100).
            if (prevCover != null)
            {
                _npOutgoingCoverStoryboard?.Stop();

                NowPlayingOutgoingCoverImage.Source = prevCover;
                NowPlayingOutgoingCoverTransform.TranslateX = 0;
                NowPlayingOutgoingCoverTransform.ScaleX = 1;
                NowPlayingOutgoingCoverTransform.ScaleY = 1;
                NowPlayingOutgoingCoverBorder.Opacity = 1;
                NowPlayingOutgoingCoverBorder.Visibility = Visibility.Visible;

                double outX = isGoingBack ? 260 : -260;
                var duration = TimeSpan.FromMilliseconds(380);
                // EaseOut : la cover sortante demarre vite (comme l'arrivante) au lieu de partir tard.
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var sb = new Storyboard();
                AddOutgoingAnimation(sb, NowPlayingOutgoingCoverTransform, "TranslateX", 0.0, outX, duration, ease);
                AddOutgoingAnimation(sb, NowPlayingOutgoingCoverTransform, "ScaleX", 1.0, 0.92, duration, ease);
                AddOutgoingAnimation(sb, NowPlayingOutgoingCoverTransform, "ScaleY", 1.0, 0.92, duration, ease);
                AddOutgoingAnimation(sb, NowPlayingOutgoingCoverBorder, "Opacity", 1.0, 0.0, TimeSpan.FromMilliseconds(300), null);
                sb.Completed += (_, _) =>
                {
                    if (!ReferenceEquals(_npOutgoingCoverStoryboard, sb)) return;
                    NowPlayingOutgoingCoverBorder.Visibility = Visibility.Collapsed;
                    NowPlayingOutgoingCoverImage.Source = null;
                };
                _npOutgoingCoverStoryboard = sb;
                sb.Begin();
            }

            // 2. Ancien fond flou : reste opaque sous le nouveau (qui monte de 0 a 1), puis disparait (StartOutgoingBlurFadeOut).
            if (prevBlur != null)
            {
                _npOutgoingBlurStoryboard?.Stop();
                NowPlayingOutgoingBlurredBackground.Source = prevBlur;
                NowPlayingOutgoingBlurredBackground.Opacity = 1;
                NowPlayingOutgoingBlurredBackground.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Purement cosmetique : aucune exception ne doit impacter le lecteur
        }
    }

    private static void AddOutgoingAnimation(Storyboard sb, DependencyObject target, string property, double from, double to, TimeSpan duration, EasingFunctionBase? easing)
    {
        var anim = new DoubleAnimation { From = from, To = to, Duration = duration };
        if (easing != null) anim.EasingFunction = easing;
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);
        sb.Children.Add(anim);
    }

    // Appele quand le NOUVEAU fond flou commence son fondu (StartNowPlayingBlurFade) : l'ancien disparait une fois le nouveau opaque.
    private void StartOutgoingBlurFadeOut()
    {
        if (NowPlayingOutgoingBlurredBackground.Visibility != Visibility.Visible) return;

        try
        {
            _npOutgoingBlurStoryboard?.Stop();

            var anim = new DoubleAnimation
            {
                To = 0.0,
                BeginTime = TimeSpan.FromMilliseconds(600),   // = duree du fondu du nouveau flou
                Duration = TimeSpan.FromMilliseconds(200)
            };
            Storyboard.SetTarget(anim, NowPlayingOutgoingBlurredBackground);
            Storyboard.SetTargetProperty(anim, "Opacity");

            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Completed += (_, _) =>
            {
                if (!ReferenceEquals(_npOutgoingBlurStoryboard, sb)) return;
                NowPlayingOutgoingBlurredBackground.Visibility = Visibility.Collapsed;
                NowPlayingOutgoingBlurredBackground.Source = null;
            };
            _npOutgoingBlurStoryboard = sb;
            sb.Begin();
        }
        catch
        {
            // Purement cosmetique
        }
    }

    // Taille de la cover du lecteur et espacement qui la separe du texte (valeurs de MainWindow.xaml).
    private const double PlayerCoverWidth = 48.0;
    private const double PlayerCoverSpacing = 10.0;

    // Repli / depli de la cover du lecteur (entree / sortie du mode grosse cover) : la colonne de gauche s'adapte, donc la barre de lecture
    // (colonne de droite) s'etire ou se raccourcit, sans trou entre les textes et le timecode.
    // La mise en page change UNE seule fois, tout de suite ; l'ecart visuel est ensuite resorbe par des animations independantes
    // (compositeur) sur le texte et sur la ligne de progression. Avant, Width + Spacing etaient animes en "dependent animation" :
    // relayout de toute la barre a chaque image (et cover re-echantillonnee a chaque largeur), donc saccades des qu'un travail
    // tombait en meme temps sur le thread UI (creation de la page grosse cover, navigation...).
    private void AddPlayerCoverCollapseAnimations(Storyboard sb, bool collapse, TimeSpan begin, TimeSpan duration)
    {
        double textLeftBefore = 0, rowWidthBefore = 0;
        bool measured = false;
        try
        {
            if (PlayerBar.IsLoaded && PlayerBar.ActualWidth > 0 && PlayerProgressRow.ActualWidth > 0)
            {
                textLeftBefore = NowPlayingTextContainer.TransformToVisual(PlayerBar).TransformPoint(new Point(0, 0)).X;
                rowWidthBefore = PlayerProgressRow.ActualWidth;
                measured = true;
            }
        }
        catch { }

        // Empreinte de la cover dans la mise en page : marge droite negative = largeur 0, la cover reste dessinee (a gauche) le temps de son fondu.
        NowPlayingCoverBorder.IsHitTestVisible = !collapse;   // invisible mais encore la : ne doit pas capter les clics sur le texte
        NowPlayingCoverBorder.Margin = collapse ? new Thickness(0, 0, -PlayerCoverWidth, 0) : new Thickness(0);
        PlayerInfoPanel.Spacing = collapse ? 0.0 : PlayerCoverSpacing;

        if (!measured) return;   // barre pas encore affichee : rien a compenser

        double textShift, rowScale;
        try
        {
            UpdateNowPlayingTextMaxWidth();
            PlayerBar.UpdateLayout();
            textShift = textLeftBefore - NowPlayingTextContainer.TransformToVisual(PlayerBar).TransformPoint(new Point(0, 0)).X;
            double rowWidthAfter = PlayerProgressRow.ActualWidth;
            rowScale = rowWidthAfter > 0 ? Math.Clamp(rowWidthBefore / rowWidthAfter, 0.5, 2.0) : 1.0;
        }
        catch
        {
            return;
        }

        // Le texte et la ligne de progression sont deja a leur place finale dans la mise en page : on les remet visuellement a l'ancienne
        // position, puis on les ramene doucement.
        if (Math.Abs(textShift) > 0.5)
            AddLayoutCompensationAnimation(sb, NowPlayingTextTransform, "X", textShift, 0.0, begin, duration);
        if (Math.Abs(rowScale - 1.0) > 0.001)
            AddLayoutCompensationAnimation(sb, PlayerProgressRowScale, "ScaleX", rowScale, 1.0, begin, duration);
    }

    // Garde la valeur de depart pendant `begin` (pas de saut visible si l'animation est differee), puis rejoint `to` en EaseOut.
    private static void AddLayoutCompensationAnimation(Storyboard sb, DependencyObject target, string property, double from, double to, TimeSpan begin, TimeSpan duration)
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = from });
        if (begin > TimeSpan.Zero)
            anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = begin, Value = from });
        anim.KeyFrames.Add(new EasingDoubleKeyFrame
        {
            KeyTime = begin + duration,
            Value = to,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);
        sb.Children.Add(anim);
    }

    // Sortie du mode grosse cover : on nettoie les calques de transition.
    private void ResetOutgoingNowPlayingLayers()
    {
        _npOutgoingCoverStoryboard?.Stop();
        _npOutgoingBlurStoryboard?.Stop();
        _npOutgoingCoverStoryboard = null;
        _npOutgoingBlurStoryboard = null;
        _npPrevCoverSource = null;
        _npPrevBlurSource = null;

        NowPlayingOutgoingCoverBorder.Visibility = Visibility.Collapsed;
        NowPlayingOutgoingCoverImage.Source = null;
        NowPlayingOutgoingBlurredBackground.Visibility = Visibility.Collapsed;
        NowPlayingOutgoingBlurredBackground.Source = null;
    }
}
