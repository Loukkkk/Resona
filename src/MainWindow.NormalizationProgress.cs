// ============================================================
//  MainWindow.NormalizationProgress.cs  —  Resona
//  Bulle de progression de l'analyse de normalisation (a la place de l'ancienne fenetre bloquante) :
//  petit encart dans un coin, l'application reste utilisable pendant l'analyse.
// ============================================================

using System;
using System.Threading;
using Microsoft.UI.Xaml;
using Resona.Models;

namespace Resona;

public sealed partial class MainWindow
{
    private DateTime _normProgressStart;
    private int      _normProgressLastShown = -1;

    /// <summary>Affiche la bulle (a appeler sur le thread de l'interface).</summary>
    private void ShowNormalizationProgress(int total)
    {
        try
        {
            _normProgressStart = DateTime.UtcNow;
            _normProgressLastShown = -1;
            bool fr = Strings.Current.IsFr;
            NormProgressTitle.Text = fr ? "Normalisation audio" : "Audio normalization";
            UpdateNormalizationProgress(0, total);
            NormProgressBubble.Visibility = Visibility.Visible;
        }
        catch { }
    }

    /// <summary>Met a jour la bulle (a appeler sur le thread de l'interface).</summary>
    private void UpdateNormalizationProgress(int done, int total)
    {
        try
        {
            if (total <= 0) return;
            done = Math.Clamp(done, 0, total);
            bool fr = Strings.Current.IsFr;
            int percent = (int)Math.Round(100.0 * done / total);

            NormProgressBar.Value = percent;
            NormProgressDetail.Text = fr
                ? $"{done} / {total} titres analysés ({percent} %)"
                : $"{done} / {total} tracks analyzed ({percent}%)";

            if (done >= 3)
            {
                double elapsed = (DateTime.UtcNow - _normProgressStart).TotalSeconds;
                double remaining = elapsed / done * (total - done);
                string eta = remaining < 60 ? $"{Math.Max(1, (int)Math.Round(remaining))} s"
                           : $"{(int)Math.Ceiling(remaining / 60.0)} min";
                NormProgressEta.Text = fr ? $"Temps restant estimé : {eta}" : $"Estimated time left: {eta}";
            }
            else
            {
                NormProgressEta.Text = fr ? "Vous pouvez continuer à utiliser Resona." : "You can keep using Resona.";
            }
        }
        catch { }
    }

    /// <summary>Masque la bulle (a appeler sur le thread de l'interface).</summary>
    private void HideNormalizationProgress()
    {
        try { NormProgressBubble.Visibility = Visibility.Collapsed; }
        catch { }
    }

    /// <summary>Appelable depuis n'importe quel thread : met la bulle a jour (au plus quelques fois par seconde).</summary>
    private void ReportNormalizationProgress(int done, int total, bool show)
    {
        if (!show) return;
        // On evite d'inonder l'interface : une mise a jour tous les ~1 % ou a la fin.
        int step = Math.Max(1, total / 100);
        int last = Volatile.Read(ref _normProgressLastShown);
        if (done < total && done - last < step) return;
        Volatile.Write(ref _normProgressLastShown, done);
        DispatcherQueue.TryEnqueue(() => UpdateNormalizationProgress(done, total));
    }
}
