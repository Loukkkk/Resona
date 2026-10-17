// ============================================================
//  PlaylistShareExporter.cs  —  Resona
//  Export d'une playlist pour la partager : page web autonome (HTML, pochettes integrees) ou image PNG (pochettes + liste des titres).
//  Aucun acces reseau : tout est genere localement a partir des pochettes deja en cache.
// ============================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Net;
using System.Text;
using Resona.Models;

namespace Resona.Services;

public sealed class PlaylistShareExporter : IDisposable
{
    /// <summary>Nombre maximum de titres dessines dans l'image PNG (au-dela, une ligne "+ N autres titres" est ajoutee).</summary>
    public const int MaxImageTracks = 100;

    /// <summary>Page GitHub de l'app : cible du lien "Resona" dans le pied de page de l'export HTML.</summary>
    public const string GitHubUrl = "https://github.com/Loukkkk/Resona";

    private readonly string _name;
    private readonly IReadOnlyList<Track> _tracks;
    private readonly string? _playlistCoverPath;
    private readonly bool _fr;
    private readonly Dictionary<string, Bitmap?> _covers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Color _accent;
    private readonly Color _background = Color.FromArgb(15, 15, 20);

    public PlaylistShareExporter(string playlistName, IReadOnlyList<Track> tracks, string? playlistCoverPath, bool isFrench)
    {
        _name = string.IsNullOrWhiteSpace(playlistName) ? "Playlist" : playlistName.Trim();
        _tracks = tracks;
        _playlistCoverPath = playlistCoverPath;
        _fr = isFrench;
        _accent = ComputeAccent();
    }

    public void Dispose()
    {
        foreach (var bmp in _covers.Values) bmp?.Dispose();
        _covers.Clear();
    }

    // ------------------------------------------------------------------ Outils communs

    private Bitmap? GetCover(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_covers.TryGetValue(path, out var cached)) return cached;

        Bitmap? bmp = null;
        try
        {
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                using var img = System.Drawing.Image.FromStream(fs);
                bmp = new Bitmap(img);   // copie : le fichier n'est pas verrouille
            }
        }
        catch { bmp = null; }

        _covers[path] = bmp;
        return bmp;
    }

    /// <summary>Pochettes (distinctes) des titres, dans l'ordre, pour la mosaique d'en-tete.</summary>
    private List<Bitmap> DistinctCovers(int max)
    {
        var list = new List<Bitmap>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var own = GetCover(_playlistCoverPath);
        if (own != null) { list.Add(own); return list; }   // pochette choisie pour la playlist : elle seule

        foreach (var t in _tracks)
        {
            if (string.IsNullOrEmpty(t.CoverArtPath) || !seen.Add(t.CoverArtPath)) continue;
            var bmp = GetCover(t.CoverArtPath);
            if (bmp != null) list.Add(bmp);
            if (list.Count >= max) break;
        }
        return list;
    }

    private static Bitmap CropResize(System.Drawing.Image src, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        double scale = Math.Max((double)w / src.Width, (double)h / src.Height);
        float sw = (float)(w / scale), sh = (float)(h / scale);
        float sx = (src.Width - sw) / 2f, sy = (src.Height - sh) / 2f;
        g.DrawImage(src, new Rectangle(0, 0, w, h), sx, sy, sw, sh, GraphicsUnit.Pixel);
        return bmp;
    }

    private static string ToJpegDataUri(System.Drawing.Image img, int size, long quality = 80L)
    {
        using var resized = CropResize(img, size, size);
        using var ms = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var ps = new EncoderParameters(1);
        ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        resized.Save(ms, codec, ps);
        return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>Couleur d'accent : couleur moyenne de la premiere pochette, rendue assez vive et lumineuse pour un fond sombre.</summary>
    private Color ComputeAccent()
    {
        Color baseColor = Color.FromArgb(124, 92, 255);
        try
        {
            var first = _tracks.Select(t => GetCover(t.CoverArtPath)).FirstOrDefault(b => b != null) ?? GetCover(_playlistCoverPath);
            if (first != null)
            {
                using var one = new Bitmap(1, 1, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(one))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(first, new Rectangle(0, 0, 1, 1));
                }
                baseColor = one.GetPixel(0, 0);
            }
        }
        catch { }

        float h = baseColor.GetHue();
        float s = Math.Max(baseColor.GetSaturation(), 0.45f);
        float l = Math.Clamp(baseColor.GetBrightness(), 0.42f, 0.60f);
        return FromHsl(h, s, l);
    }

    private static Color FromHsl(float h, float s, float l)
    {
        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float hp = h / 60f;
        float x = c * (1f - Math.Abs(hp % 2f - 1f));
        float r1 = 0, g1 = 0, b1 = 0;
        if (hp < 1) { r1 = c; g1 = x; }
        else if (hp < 2) { r1 = x; g1 = c; }
        else if (hp < 3) { g1 = c; b1 = x; }
        else if (hp < 4) { g1 = x; b1 = c; }
        else if (hp < 5) { r1 = x; b1 = c; }
        else { r1 = c; b1 = x; }
        float m = l - c / 2f;
        return Color.FromArgb((int)((r1 + m) * 255), (int)((g1 + m) * 255), (int)((b1 + m) * 255));
    }

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    private static string FormatTrackDuration(TimeSpan d)
        => d.TotalHours >= 1 ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}" : $"{d.Minutes}:{d.Seconds:00}";

    private string FormatTotal()
    {
        var total = TimeSpan.FromSeconds(_tracks.Sum(t => t.Duration.TotalSeconds));
        string count = _fr ? $"{_tracks.Count} titre{(_tracks.Count > 1 ? "s" : "")}" : $"{_tracks.Count} track{(_tracks.Count > 1 ? "s" : "")}";
        string dur = total.TotalHours >= 1 ? $"{(int)total.TotalHours} h {total.Minutes:00} min" : $"{Math.Max(1, (int)Math.Round(total.TotalMinutes))} min";
        return $"{count} • {dur}";
    }

    // ------------------------------------------------------------------ Page web

    /// <summary>Page HTML autonome (un seul fichier, pochettes integrees en base64), adaptee au telephone et a l'impression.</summary>
    public string BuildHtml()
    {
        var sb = new StringBuilder(64 * 1024);
        string accent = Hex(_accent);
        string kicker = _fr ? "PLAYLIST" : "PLAYLIST";
        string madeWith = _fr ? "Créée avec" : "Made with";   // suivi du lien "Resona" (GitHub) dans le pied de page
        string date = DateTime.Now.ToString("d MMMM yyyy", _fr ? new CultureInfo("fr-FR") : new CultureInfo("en-US"));

        sb.Append("<!DOCTYPE html>\n<html lang=\"").Append(_fr ? "fr" : "en").Append("\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(WebUtility.HtmlEncode(_name)).Append("</title>\n<style>\n");
        sb.Append(":root{--accent:").Append(accent).Append(";--bg:").Append(Hex(_background)).Append(";--text:#f4f4f8;--muted:#a3a3b2;--line:rgba(255,255,255,.07)}\n");
        sb.Append("*{box-sizing:border-box}html{-webkit-text-size-adjust:100%}\n");
        sb.Append("body{margin:0;background:var(--bg);color:var(--text);font-family:'Segoe UI',system-ui,-apple-system,Roboto,'Helvetica Neue',Arial,sans-serif;line-height:1.35}\n");
        sb.Append(".hero{padding:56px 24px 32px;background:linear-gradient(180deg,color-mix(in srgb,var(--accent) 55%,transparent) 0%,transparent 100%)}\n");
        sb.Append(".hero-in{max-width:960px;margin:0 auto;display:flex;gap:32px;align-items:center}\n");
        sb.Append(".mosaic{flex:0 0 220px;width:220px;height:220px;border-radius:12px;overflow:hidden;box-shadow:0 18px 48px rgba(0,0,0,.45);background:var(--accent);display:grid;grid-template-columns:1fr 1fr;grid-template-rows:1fr 1fr}\n");
        sb.Append(".mosaic.one{display:block}.mosaic img{width:100%;height:100%;object-fit:cover;display:block}\n");
        sb.Append(".mosaic .ph{grid-column:1/-1;grid-row:1/-1;display:flex;align-items:center;justify-content:center;font-size:72px;color:rgba(255,255,255,.55)}\n");
        sb.Append(".kicker{margin:0 0 6px;font-size:12px;letter-spacing:.14em;font-weight:700;color:var(--muted)}\n");
        sb.Append("h1{margin:0 0 10px;font-size:clamp(28px,6vw,56px);line-height:1.05;font-weight:800;word-break:break-word}\n");
        sb.Append(".sub{margin:0;color:var(--muted);font-size:15px}\n");
        sb.Append("main{max-width:960px;margin:0 auto;padding:8px 24px 40px}\n");
        sb.Append("table{width:100%;border-collapse:collapse}\n");
        sb.Append("th{text-align:left;font-size:11px;letter-spacing:.1em;text-transform:uppercase;color:var(--muted);font-weight:600;padding:10px 8px;border-bottom:1px solid var(--line)}\n");
        sb.Append("td{padding:8px;border-bottom:1px solid var(--line);vertical-align:middle}\n");
        sb.Append("tr:hover td{background:rgba(255,255,255,.035)}\n");
        sb.Append(".n{width:36px;color:var(--muted);text-align:right;font-variant-numeric:tabular-nums}\n");
        sb.Append(".c{width:56px}.c img,.c .ph{width:44px;height:44px;border-radius:6px;object-fit:cover;display:block;background:rgba(255,255,255,.08)}\n");
        sb.Append(".t b{display:block;font-weight:600;font-size:15px;overflow-wrap:anywhere}.t span{display:block;color:var(--muted);font-size:13px;overflow-wrap:anywhere}\n");
        sb.Append(".al{color:var(--muted);font-size:13px;overflow-wrap:anywhere}.d{width:64px;text-align:right;color:var(--muted);font-variant-numeric:tabular-nums;font-size:13px}\n");
        sb.Append("footer{max-width:960px;margin:0 auto;padding:8px 24px 48px;color:var(--muted);font-size:12px}footer a{color:var(--accent);font-weight:700;text-decoration:none}footer a:hover{text-decoration:underline}\n");
        sb.Append("@media(max-width:640px){.hero{padding:32px 16px 20px}.hero-in{flex-direction:column;align-items:flex-start;gap:20px}.mosaic{width:180px;height:180px;flex-basis:auto}.al,th.al{display:none}main,footer{padding-left:12px;padding-right:12px}.n{width:28px}}\n");
        sb.Append("@media print{body{background:#fff;color:#000}.hero{background:none}.mosaic{box-shadow:none}.sub,.t span,.al,.d,.n,.kicker,th,footer{color:#444}tr:hover td{background:none}}\n");
        sb.Append("</style>\n</head>\n<body>\n");

        // En-tete : mosaique + titre
        var heroCovers = DistinctCovers(4);
        sb.Append("<header class=\"hero\"><div class=\"hero-in\">\n");
        if (heroCovers.Count >= 4)
        {
            sb.Append("<div class=\"mosaic\">");
            for (int i = 0; i < 4; i++) sb.Append("<img alt=\"\" src=\"").Append(ToJpegDataUri(heroCovers[i], 240)).Append("\">");
            sb.Append("</div>\n");
        }
        else if (heroCovers.Count > 0)
        {
            sb.Append("<div class=\"mosaic one\"><img alt=\"\" src=\"").Append(ToJpegDataUri(heroCovers[0], 440)).Append("\"></div>\n");
        }
        else
        {
            sb.Append("<div class=\"mosaic one\"><div class=\"ph\">♪</div></div>\n");
        }
        sb.Append("<div><p class=\"kicker\">").Append(kicker).Append("</p><h1>").Append(WebUtility.HtmlEncode(_name)).Append("</h1><p class=\"sub\">")
          .Append(WebUtility.HtmlEncode(FormatTotal())).Append("</p></div>\n</div></header>\n");

        // Liste des titres (une miniature par pochette distincte : encodee une seule fois)
        sb.Append("<main><table>\n<thead><tr><th class=\"n\">#</th><th class=\"c\"></th><th>").Append(_fr ? "Titre" : "Title")
          .Append("</th><th class=\"al\">").Append(_fr ? "Album" : "Album").Append("</th><th class=\"d\">").Append(_fr ? "Durée" : "Time").Append("</th></tr></thead>\n<tbody>\n");

        var thumbs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _tracks.Count; i++)
        {
            var t = _tracks[i];
            string? thumb = null;
            if (!string.IsNullOrEmpty(t.CoverArtPath))
            {
                if (!thumbs.TryGetValue(t.CoverArtPath, out thumb))
                {
                    var bmp = GetCover(t.CoverArtPath);
                    thumb = bmp != null ? ToJpegDataUri(bmp, 96, 78L) : null;
                    thumbs[t.CoverArtPath] = thumb;
                }
            }

            sb.Append("<tr><td class=\"n\">").Append(i + 1).Append("</td><td class=\"c\">");
            if (thumb != null) sb.Append("<img alt=\"\" loading=\"lazy\" src=\"").Append(thumb).Append("\">");
            else sb.Append("<div class=\"ph\"></div>");
            sb.Append("</td><td class=\"t\"><b>").Append(WebUtility.HtmlEncode(t.Title ?? "")).Append("</b><span>").Append(WebUtility.HtmlEncode(t.Artist ?? ""))
              .Append("</span></td><td class=\"al\">").Append(WebUtility.HtmlEncode(t.Album ?? "")).Append("</td><td class=\"d\">")
              .Append(FormatTrackDuration(t.Duration)).Append("</td></tr>\n");
        }

        sb.Append("</tbody></table></main>\n<footer>").Append(WebUtility.HtmlEncode(madeWith)).Append(" <a href=\"").Append(GitHubUrl).Append("\" target=\"_blank\" rel=\"noopener\">Resona</a> • ").Append(WebUtility.HtmlEncode(date)).Append("</footer>\n</body>\n</html>\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ Image PNG

    /// <summary>Image PNG de 1080 px de large : en-tete (mosaique, titre, infos) puis une ligne par titre avec sa pochette.</summary>
    public byte[] RenderPng()
    {
        const int W = 1080, Margin = 60, HeaderH = 380, RowH = 84, Thumb = 64, FooterH = 110;
        int shown = Math.Min(_tracks.Count, MaxImageTracks);
        int extra = _tracks.Count - shown;
        int H = HeaderH + shown * RowH + (extra > 0 ? 50 : 0) + FooterH;

        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Fond : degrade de la couleur d'accent vers le fond sombre
        g.Clear(_background);
        using (var grad = new LinearGradientBrush(new Rectangle(0, 0, W, HeaderH + 120), Color.FromArgb(255, Blend(_accent, _background, 0.35)), _background, 90f))
        {
            g.FillRectangle(grad, 0, 0, W, HeaderH + 120);
        }

        // Mosaique / pochette (280 x 280)
        var heroCovers = DistinctCovers(4);
        int cx = Margin, cy = 50, cs = 280;
        using (var path = RoundedRect(new RectangleF(cx, cy, cs, cs), 18))
        {
            g.SetClip(path);
            if (heroCovers.Count >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    using var part = CropResize(heroCovers[i], cs / 2, cs / 2);
                    g.DrawImage(part, cx + (i % 2) * (cs / 2), cy + (i / 2) * (cs / 2), cs / 2, cs / 2);
                }
            }
            else if (heroCovers.Count > 0)
            {
                using var one = CropResize(heroCovers[0], cs, cs);
                g.DrawImage(one, cx, cy, cs, cs);
            }
            else
            {
                using var b = new SolidBrush(_accent);
                g.FillRectangle(b, cx, cy, cs, cs);
                using var f = new Font("Segoe UI Symbol", 90f, FontStyle.Regular, GraphicsUnit.Pixel);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("♪", f, Brushes.White, new RectangleF(cx, cy, cs, cs), sf);
            }
            g.ResetClip();
        }

        // Textes d'en-tete
        int tx = cx + cs + 44, tw = W - tx - Margin;
        using (var kickerFont = new Font("Segoe UI Semibold", 18f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var titleFont = new Font("Segoe UI", 52f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var subFont = new Font("Segoe UI", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var muted = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
        using (var white = new SolidBrush(Color.White))
        {
            // Bloc de texte (PLAYLIST / titre / infos) empile sans trou, puis centre verticalement sur la pochette.
            using var wrap = new StringFormat { Trimming = StringTrimming.EllipsisWord };
            const float kickerH = 24f, subH = 34f, gapKicker = 8f, gapSub = 10f, maxTitleH = 140f;
            float titleH = Math.Min(maxTitleH, (float)Math.Ceiling(g.MeasureString(_name, titleFont, new SizeF(tw, maxTitleH), wrap).Height));
            float blockH = kickerH + gapKicker + titleH + gapSub + subH;
            float top = cy + (cs - blockH) / 2f;

            g.DrawString("PLAYLIST", kickerFont, muted, new RectangleF(tx, top, tw, kickerH));
            g.DrawString(_name, titleFont, white, new RectangleF(tx, top + kickerH + gapKicker, tw, titleH), wrap);
            using var one = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(FormatTotal(), subFont, muted, new RectangleF(tx, top + kickerH + gapKicker + titleH + gapSub, tw, subH), one);
        }

        // Lignes de titres
        using var numFont = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleF = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var artistF = new Font("Segoe UI", 19f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var durF = new Font("Segoe UI", 19f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(244, 244, 248));
        using var mutedBrush = new SolidBrush(Color.FromArgb(163, 163, 178));
        using var zebra = new SolidBrush(Color.FromArgb(10, 255, 255, 255));
        using var ellipsis = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        int y = HeaderH;
        for (int i = 0; i < shown; i++, y += RowH)
        {
            var t = _tracks[i];
            if (i % 2 == 0) g.FillRectangle(zebra, 0, y, W, RowH);

            int ty = y + (RowH - Thumb) / 2;
            using (var path = RoundedRect(new RectangleF(Margin, ty, Thumb, Thumb), 8))
            {
                var cover = GetCover(t.CoverArtPath);
                if (cover != null)
                {
                    g.SetClip(path);
                    using var small = CropResize(cover, Thumb, Thumb);
                    g.DrawImage(small, Margin, ty, Thumb, Thumb);
                    g.ResetClip();
                }
                else
                {
                    using var ph = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
                    g.FillPath(ph, path);
                }
            }

            int textX = Margin + Thumb + 20;
            int durW = 110;
            int textW = W - Margin - durW - textX;
            g.DrawString(t.Title ?? "", titleF, textBrush, new RectangleF(textX, y + 10, textW, 32), ellipsis);
            string sub = string.IsNullOrWhiteSpace(t.Album) ? (t.Artist ?? "") : $"{t.Artist} • {t.Album}";
            g.DrawString(sub, artistF, mutedBrush, new RectangleF(textX, y + 42, textW, 28), ellipsis);
            g.DrawString(FormatTrackDuration(t.Duration), durF, mutedBrush, new RectangleF(W - Margin - durW, y, durW, RowH), right);
        }

        if (extra > 0)
        {
            string more = _fr ? $"+ {extra} autre{(extra > 1 ? "s" : "")} titre{(extra > 1 ? "s" : "")}" : $"+ {extra} more track{(extra > 1 ? "s" : "")}";
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(more, artistF, mutedBrush, new RectangleF(0, y, W, 50), sf);
            y += 50;
        }

        // Pied de page
        using (var footFont = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var accentBrush = new SolidBrush(_accent))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(_fr ? "Créée avec Resona" : "Made with Resona", footFont, accentBrush, new RectangleF(0, y + 10, W, FooterH - 20), sf);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static Color Blend(Color a, Color b, double amountOfA)
        => Color.FromArgb(
            (int)(a.R * amountOfA + b.R * (1 - amountOfA)),
            (int)(a.G * amountOfA + b.G * (1 - amountOfA)),
            (int)(a.B * amountOfA + b.B * (1 - amountOfA)));

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
}
