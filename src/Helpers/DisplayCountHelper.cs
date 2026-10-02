using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace Resona.Helpers
{
    /// <summary>
    /// Sélecteur "nombre de cartes affichées" partagé par les pages Albums, Artistes,
    /// Genres, Dossiers et Playlists (-1 = tout afficher).
    /// </summary>
    public static class DisplayCountHelper
    {
        private static readonly int[] DefaultOptions = { 25, 50, 100, 200, 500 };

        /// <summary>Nombre de cartes par page effectif (limite &lt;= 0 : tout sur une seule page).</summary>
        public static int GetEffective(int limit, int total) => limit > 0 ? limit : Math.Max(1, total);

        public static void Setup(ComboBox combo, int current, Action<int> onChanged, int[]? customOptions = null)
        {
            bool loading = true;

            // Options standard + la valeur actuelle si elle n'en fait pas partie, "Tout afficher" en dernier
            var values = new List<int>(customOptions ?? DefaultOptions);
            if (current > 0 && !values.Contains(current)) values.Add(current);
            values.Sort();
            values.Add(-1);

            combo.Items.Clear();
            ComboBoxItem? selected = null;
            foreach (int v in values)
            {
                var item = new ComboBoxItem
                {
                    Tag = v.ToString(),
                    Content = v > 0
                        ? (Models.Strings.Current.IsFr ? $"{v} cartes" : $"{v} cards")
                        : Models.Strings.Current.LibraryPage_Content_Toutafficher
                };
                combo.Items.Add(item);
                if (v == current) selected = item;
            }
            combo.SelectedItem = selected ?? combo.Items.Last();

            ToolTipService.SetToolTip(combo, Models.Strings.Current.IsFr
                ? "Nombre de cartes affichées par page"
                : "Number of cards shown per page");

            combo.SelectionChanged += (s, e) =>
            {
                if (loading) return;
                if (combo.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out int value))
                    onChanged(value);
            };
            loading = false;
        }
    }
}
