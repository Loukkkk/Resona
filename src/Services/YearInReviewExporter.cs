// ============================================================
//  YearInReviewExporter.cs  —  Resona
//  Carte "Year in review" en PNG vertical (1080 x 1920), prete a partager. Genere localement (aucun acces reseau),
//  dans le meme esprit que PlaylistShareExporter : GDI+ (System.Drawing), pochettes lues depuis le cache de l'app.
// ============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Resona.Services;

public sealed class YearInReviewExporter : IDisposable
{
    private const int W = 1080, H = 1920, M = 72;

    private readonly YearInReviewData _d;
    private readonly IReadOnlyDictionary<string, string?> _coverPaths;
    private readonly bool _fr;
    private readonly CultureInfo _culture;
    private readonly Dictionary<string, Bitmap?> _covers = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Color Accent = Color.FromArgb(150, 118, 255);
    private static readonly Color Muted = Color.FromArgb(170, 255, 255, 255);

    /// <param name="coverPathByTrackId">Chemin de la pochette (ou null) pour chaque identifiant de titre de la bibliotheque.</param>
    public YearInReviewExporter(YearInReviewData data, IReadOnlyDictionary<string, string?> coverPathByTrackId, bool isFrench)
    {
        _d = data;
        _coverPaths = coverPathByTrackId;
        _fr = isFrench;
        _culture = CultureInfo.GetCultureInfo(isFrench ? "fr-FR" : "en-US");
    }

    public void Dispose()
    {
        foreach (var b in _covers.Values) b?.Dispose();
        _covers.Clear();
    }

    private string L(string fr, string en) => _fr ? fr : en;

    // ------------------------------------------------------------------ outils

    private Bitmap? CoverOf(string trackId)
    {
        if (string.IsNullOrEmpty(trackId) || !_coverPaths.TryGetValue(trackId, out var path) || string.IsNullOrEmpty(path)) return null;
        if (_covers.TryGetValue(path, out var cached)) return cached;
        Bitmap? bmp = null;
        try
        {
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                using var img = System.Drawing.Image.FromStream(fs);
                bmp = new Bitmap(img);
            }
        }
        catch { bmp = null; }
        _covers[path] = bmp;
        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static void DrawText(Graphics g, string text, Font font, Brush brush, RectangleF rect,
        StringAlignment align = StringAlignment.Near, StringAlignment vAlign = StringAlignment.Near, bool wrap = false)
    {
        using var fmt = new StringFormat
        {
            Alignment = align,
            LineAlignment = vAlign,
            Trimming = wrap ? StringTrimming.EllipsisWord : StringTrimming.EllipsisCharacter,
            FormatFlags = wrap ? (StringFormatFlags)0 : StringFormatFlags.NoWrap
        };
        g.DrawString(text, font, brush, rect, fmt);
    }

    /// <summary>Pochette arrondie (rogne en carre) ou, a defaut, une tuile neutre avec une note de musique.</summary>
    private void DrawCover(Graphics g, string trackId, RectangleF r, float radius)
    {
        using var path = RoundedRect(r, radius);
        var bmp = CoverOf(trackId);
        if (bmp != null)
        {
            g.SetClip(path, CombineMode.Replace);
            double scale = Math.Max(r.Width / bmp.Width, r.Height / bmp.Height);
            float sw = (float)(r.Width / scale), sh = (float)(r.Height / scale);
            float sx = (bmp.Width - sw) / 2f, sy = (bmp.Height - sh) / 2f;
            g.DrawImage(bmp, r, new RectangleF(sx, sy, sw, sh), GraphicsUnit.Pixel);
            g.ResetClip();
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
            g.FillPath(fill, path);
            using var f = new Font("Segoe UI Symbol", r.Height * 0.45f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
            DrawText(g, "\u266B", f, b, r, StringAlignment.Center, StringAlignment.Center);
        }
    }

    private string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours:N0} h {ts.Minutes:00} min";
        return $"{(int)ts.TotalMinutes} min";
    }

    private string FormatCount(int n) => n.ToString("N0", _culture);

    // ------------------------------------------------------------------ rendu

    public byte[] RenderPng()
    {
        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // --- Fond : degrade sombre + halos de couleur ---
        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(38, 22, 78), Color.FromArgb(12, 12, 20), 80f))
            g.FillRectangle(bg, 0, 0, W, H);
        using (var glow = new SolidBrush(Color.FromArgb(34, Accent)))
        {
            g.FillEllipse(glow, -260, -300, 900, 900);
            g.FillEllipse(glow, 600, 1250, 800, 800);
        }

        using var white = new SolidBrush(Color.White);
        using var muted = new SolidBrush(Muted);
        using var accent = new SolidBrush(Accent);
        using var card = new SolidBrush(Color.FromArgb(28, 255, 255, 255));

        // --- En-tete ---
        using (var f = new Font("Segoe UI Semibold", 26f, FontStyle.Regular, GraphicsUnit.Pixel))
            DrawText(g, "RESONA", f, accent, new RectangleF(M, 70, W - 2 * M, 36));
        using (var f = new Font("Segoe UI", 210f, FontStyle.Bold, GraphicsUnit.Pixel))
            DrawText(g, _d.Year.ToString(CultureInfo.InvariantCulture), f, white, new RectangleF(M - 8, 100, W - 2 * M, 250));
        using (var f = new Font("Segoe UI Semibold", 46f, FontStyle.Regular, GraphicsUnit.Pixel))
            DrawText(g, L("Votre année en musique", "Your year in music"), f, white, new RectangleF(M, 350, W - 2 * M, 60));

        string coverage = "";
        if (_d.IsPartial && _d.CoverageStart.HasValue)
            coverage = L("Depuis le ", "Since ") + _d.CoverageStart.Value.ToString("d MMM yyyy", _culture);
        else if (_d.IsOngoing)
            coverage = L("À ce jour", "So far");
        if (coverage.Length > 0)
            using (var f = new Font("Segoe UI", 26f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, coverage, f, muted, new RectangleF(M, 418, W - 2 * M, 36));

        // --- Chiffres cles ---
        float cardW = (W - 2 * M - 24) / 2f;
        for (int i = 0; i < 2; i++)
        {
            var r = new RectangleF(M + i * (cardW + 24), 480, cardW, 170);
            using (var p = RoundedRect(r, 26)) g.FillPath(card, p);
            string value = i == 0 ? FormatDuration(_d.TotalSeconds) : FormatCount(_d.TotalPlays);
            string label = i == 0 ? L("d'écoute", "listened") : L("écoutes", "plays");
            using (var f = new Font("Segoe UI", value.Length > 9 ? 50f : 64f, FontStyle.Bold, GraphicsUnit.Pixel))
                DrawText(g, value, f, white, new RectangleF(r.X + 28, r.Y + 22, r.Width - 56, 80));
            using (var f = new Font("Segoe UI", 28f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, label, f, muted, new RectangleF(r.X + 28, r.Y + 106, r.Width - 56, 40));
        }
        using (var f = new Font("Segoe UI", 26f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            string line = string.Format(_culture, L("{0} titres différents  •  {1} artistes  •  {2} jours d'écoute", "{0} different tracks  •  {1} artists  •  {2} listening days"),
                FormatCount(_d.DistinctTracks), FormatCount(_d.DistinctArtists), FormatCount(_d.ActiveDays));
            DrawText(g, line, f, muted, new RectangleF(M, 676, W - 2 * M, 36), StringAlignment.Near);
        }

        // --- Titre n°1 ---
        float y = 748;
        using (var f = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
            DrawText(g, L("TITRE N°1", "TOP TRACK"), f, accent, new RectangleF(M, y, W - 2 * M, 32));
        y += 46;
        if (_d.TopTracks.Count > 0)
        {
            var t = _d.TopTracks[0];
            DrawCover(g, t.TrackId, new RectangleF(M, y, 300, 300), 24);
            float tx = M + 300 + 36, tw = W - M - tx;
            using (var f = new Font("Segoe UI", 50f, FontStyle.Bold, GraphicsUnit.Pixel))
                DrawText(g, t.Title, f, white, new RectangleF(tx, y + 20, tw, 130), StringAlignment.Near, StringAlignment.Near, wrap: true);
            using (var f = new Font("Segoe UI", 30f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, string.IsNullOrEmpty(t.Artist) ? "" : t.Artist, f, muted, new RectangleF(tx, y + 160, tw, 42));
            using (var f = new Font("Segoe UI Semibold", 34f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, string.Format(_culture, L("{0} écoutes", "{0} plays"), FormatCount(t.Plays)), f, accent, new RectangleF(tx, y + 232, tw, 46));
        }
        y += 300 + 44;

        // --- Titres 2 a 5 ---
        using (var numF = new Font("Segoe UI Semibold", 26f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var titleF = new Font("Segoe UI Semibold", 28f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var artistF = new Font("Segoe UI", 22f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var playsF = new Font("Segoe UI", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            for (int i = 1; i < Math.Min(5, _d.TopTracks.Count); i++)
            {
                var t = _d.TopTracks[i];
                float ry = y + (i - 1) * 76;
                DrawText(g, (i + 1).ToString(CultureInfo.InvariantCulture), numF, muted, new RectangleF(M, ry, 40, 64), StringAlignment.Near, StringAlignment.Center);
                DrawCover(g, t.TrackId, new RectangleF(M + 52, ry, 64, 64), 10);
                float nx = M + 52 + 64 + 22, right = W - M - 190;
                DrawText(g, t.Title, titleF, white, new RectangleF(nx, ry - 1, right - nx, 36));
                DrawText(g, t.Artist ?? "", artistF, muted, new RectangleF(nx, ry + 34, right - nx, 30));
                DrawText(g, string.Format(_culture, L("{0} écoutes", "{0} plays"), FormatCount(t.Plays)), playsF, muted,
                    new RectangleF(right, ry, 190, 64), StringAlignment.Far, StringAlignment.Center);
            }
        }
        y += 4 * 76 + 18;

        // --- Artistes / genre ---
        if (_d.TopArtists.Count > 0)
        {
            using (var f = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, L("ARTISTES LES PLUS ÉCOUTÉS", "TOP ARTISTS"), f, accent, new RectangleF(M, y, W - 2 * M, 32));
            using (var f = new Font("Segoe UI", 32f, FontStyle.Bold, GraphicsUnit.Pixel))
                DrawText(g, string.Join("  •  ", _d.TopArtists.Take(3).Select(a => a.Name)), f, white, new RectangleF(M, y + 40, W - 2 * M, 46));
            y += 98;
        }
        if (_d.TopGenres.Count > 0)
        {
            using (var f = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawText(g, L("GENRE FAVORI", "TOP GENRE"), f, accent, new RectangleF(M, y, W - 2 * M, 32));
            using (var f = new Font("Segoe UI", 32f, FontStyle.Bold, GraphicsUnit.Pixel))
                DrawText(g, _d.TopGenres[0].Name, f, white, new RectangleF(M, y + 40, W - 2 * M, 46));
        }

        // --- Graphique par mois ---
        float baseline = 1798, maxBar = 120, barW = (W - 2 * M - 11 * 14) / 12f;
        double maxMonth = Math.Max(1, _d.MonthSeconds.Max());
        int bestMonth = Array.IndexOf(_d.MonthSeconds, _d.MonthSeconds.Max());
        using (var f = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            for (int m = 0; m < 12; m++)
            {
                float bx = M + m * (barW + 14);
                float bh = (float)(_d.MonthSeconds[m] / maxMonth * maxBar);
                if (_d.MonthSeconds[m] > 0) bh = Math.Max(bh, 8f);
                var barRect = new RectangleF(bx, baseline - Math.Max(bh, 4f), barW, Math.Max(bh, 4f));
                using (var p = RoundedRect(barRect, 6))
                using (var br = new SolidBrush(m == bestMonth && _d.MonthSeconds[m] > 0 ? Accent : Color.FromArgb(70, 255, 255, 255)))
                    g.FillPath(br, p);
                string label = _culture.DateTimeFormat.GetAbbreviatedMonthName(m + 1).TrimEnd('.');
                label = label.Length > 0 ? char.ToUpper(label[0], _culture).ToString() : "";
                DrawText(g, label, f, muted, new RectangleF(bx - 4, baseline + 6, barW + 8, 26), StringAlignment.Center);
            }
        }

        // --- Pied de page ---
        using (var f = new Font("Segoe UI", 22f, FontStyle.Regular, GraphicsUnit.Pixel))
            DrawText(g, L("Créé avec Resona", "Made with Resona"), f, accent, new RectangleF(0, 1862, W, 30), StringAlignment.Center);

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
