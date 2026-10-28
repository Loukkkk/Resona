// ============================================================
//  SmartPlaylistEditor.cs  —  Resona
//  Boite de dialogue de creation / edition d'une playlist intelligente (regles dynamiques).
//  Construite en code (comme les autres dialogues de PlaylistsPage), sans XAML.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;
using Resona.Services;

namespace Resona.Views;

public static class SmartPlaylistEditor
{
    public sealed record Result(string Name, SmartPlaylistDefinition Definition);

    private sealed class Row
    {
        public SmartRule Rule = new();
        public Grid Panel = new();
        public ComboBox FieldBox = new();
        public ComboBox OpBox = new();
        public TextBox V1 = new();
        public TextBox V2 = new();
        public ComboBox VibeBox = new();
        public SmartOp[] Ops = Array.Empty<SmartOp>();
        public bool Building;
    }

    private static readonly SmartField[] AllFields = (SmartField[])Enum.GetValues(typeof(SmartField));

    /// <summary>Ouvre l'editeur. Renvoie null si l'utilisateur annule.</summary>
    public static async Task<Result?> ShowAsync(XamlRoot xamlRoot, string? initialName, SmartPlaylistDefinition? initial,
        IReadOnlyList<Track> library, SmartContext ctx)
    {
        bool fr = Strings.Current.IsFr;
        var def = initial ?? new SmartPlaylistDefinition();
        var rows = new List<Row>();

        var nameBox = new TextBox
        {
            Header = fr ? "Nom de la playlist" : "Playlist name",
            Text = initialName ?? (fr ? "Ma playlist intelligente" : "My smart playlist"),
        };

        // --- Modeles ---
        var presets = SmartPlaylistEngine.Presets();
        var presetBox = new ComboBox { Header = fr ? "Partir d'un modèle (optionnel)" : "Start from a template (optional)", HorizontalAlignment = HorizontalAlignment.Stretch };
        presetBox.Items.Add(fr ? "— Aucun —" : "— None —");
        foreach (var p in presets) presetBox.Items.Add(fr ? p.Fr : p.En);
        presetBox.SelectedIndex = 0;

        // --- Mode de correspondance ---
        var matchBox = new ComboBox { Header = fr ? "Les titres doivent respecter" : "Tracks must match", HorizontalAlignment = HorizontalAlignment.Stretch };
        matchBox.Items.Add(fr ? "Toutes les règles (ET)" : "All rules (AND)");
        matchBox.Items.Add(fr ? "Au moins une règle (OU)" : "At least one rule (OR)");
        matchBox.SelectedIndex = def.MatchAll ? 0 : 1;

        var rulesPanel = new StackPanel { Spacing = 8 };
        var addRuleBtn = new Button { Content = fr ? "+ Ajouter une règle" : "+ Add a rule" };

        // --- Tri et limite ---
        var sortBox = new ComboBox { Header = fr ? "Trier par" : "Sort by", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var s in SmartPlaylistEngine.SortOptions) sortBox.Items.Add(fr ? s.Fr : s.En);
        int sortIdx = Array.FindIndex(SmartPlaylistEngine.SortOptions, s => s.Key == def.Sort);
        sortBox.SelectedIndex = sortIdx < 0 ? 0 : sortIdx;

        var limitBox = new NumberBox
        {
            Header = fr ? "Limite de titres (0 = illimité)" : "Track limit (0 = unlimited)",
            Minimum = 0, Maximum = 100000, Value = Math.Max(0, def.Limit),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var preview = new TextBlock { Opacity = 0.7, TextWrapping = TextWrapping.Wrap };

        // --- Analyse d'ambiance (BPM / energie / vibe) : etat + lancement de l'analyse ---
        var moodStatus = new TextBlock { Opacity = 0.75, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        var moodBtn = new Button { Content = fr ? "Analyser la bibliothèque" : "Analyze library", HorizontalAlignment = HorizontalAlignment.Left };
        var moodBar = new ProgressBar { Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed };
        var moodPanel = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        moodPanel.Children.Add(moodStatus);
        moodPanel.Children.Add(moodBtn);
        moodPanel.Children.Add(moodBar);
        var moodCts = new System.Threading.CancellationTokenSource();
        bool moodRunning = false;

        var dialog = new ContentDialog
        {
            Title = initial == null ? (fr ? "Nouvelle playlist intelligente" : "New smart playlist") : (fr ? "Modifier les règles" : "Edit rules"),
            PrimaryButtonText = initial == null ? (fr ? "Créer" : "Create") : (fr ? "Enregistrer" : "Save"),
            CloseButtonText = fr ? "Annuler" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };
        // Largeur fixe : min = max, le popup ne se redimensionne plus selon le contenu des selecteurs.
        dialog.Resources["ContentDialogMinWidth"] = 780.0;
        dialog.Resources["ContentDialogMaxWidth"] = 780.0;
        dialog.Closed += (_, _) => moodCts.Cancel();

        // ------------------------------------------------------------ logique
        SmartPlaylistDefinition Collect()
        {
            return new SmartPlaylistDefinition
            {
                MatchAll = matchBox.SelectedIndex != 1,
                Rules = rows.Select(r => new SmartRule { Field = r.Rule.Field, Op = r.Rule.Op, Value = r.Rule.Value, Value2 = r.Rule.Value2 }).ToList(),
                Sort = SmartPlaylistEngine.SortOptions[Math.Clamp(sortBox.SelectedIndex, 0, SmartPlaylistEngine.SortOptions.Length - 1)].Key,
                Limit = double.IsNaN(limitBox.Value) ? 0 : (int)limitBox.Value,
            };
        }

        void Refresh()
        {
            bool ok = !string.IsNullOrWhiteSpace(nameBox.Text) && rows.Count > 0;
            dialog.IsPrimaryButtonEnabled = ok;
            try
            {
                var currentDef = Collect();
                int n = SmartPlaylistEngine.Evaluate(currentDef, library, ctx).Count;
                RefreshMood(currentDef);
                preview.Text = rows.Count == 0
                    ? (fr ? "Ajoutez au moins une règle." : "Add at least one rule.")
                    : (fr ? $"{n} titre(s) correspondent actuellement à ces règles." : $"{n} track(s) currently match these rules.");
            }
            catch { preview.Text = string.Empty; }
        }

        void RefreshMood(SmartPlaylistDefinition d)
        {
            bool uses = SmartPlaylistEngine.UsesMood(d);
            moodPanel.Visibility = uses ? Visibility.Visible : Visibility.Collapsed;
            if (!uses || moodRunning) return;
            int total = library.Where(t => !t.IsExternal && !string.IsNullOrEmpty(t.FilePath)).Select(t => MoodAnalysisService.Key(t.FilePath)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int pending = App.MoodAnalysis.Pending(library).Count;
            moodBtn.IsEnabled = pending > 0;
            moodStatus.Text = pending == 0
                ? (fr ? "Tous les titres sont analysés (tempo, énergie, ambiance)." : "All tracks are analyzed (tempo, energy, mood).")
                : (fr ? $"{total - pending} / {total} titres analysés. Les titres non analysés sont ignorés par les règles d'ambiance. L'analyse lit d'abord les tags du fichier (BPM, énergie, mood), puis calcule le reste localement."
                      : $"{total - pending} / {total} tracks analyzed. Unanalyzed tracks are ignored by mood rules. Analysis reads file tags first (BPM, energy, mood), then computes the rest locally.");
        }

        moodBtn.Click += async (_, _) =>
        {
            if (moodRunning) return;
            moodRunning = true;
            moodBtn.IsEnabled = false;
            moodBar.Visibility = Visibility.Visible;
            moodBar.Value = 0;
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                moodBar.Maximum = Math.Max(1, p.Total);
                moodBar.Value = p.Done;
                moodStatus.Text = fr ? $"Analyse en cours… {p.Done} / {p.Total}" : $"Analyzing… {p.Done} / {p.Total}";
            });
            try { await App.MoodAnalysis.AnalyzeAsync(library, progress, moodCts.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Resona.Services.LogService.Write("MoodAnalysis", ex, "Analyse depuis l'editeur"); }
            finally
            {
                moodRunning = false;
                moodBar.Visibility = Visibility.Collapsed;
                if (!moodCts.IsCancellationRequested)
                {
                    ctx = SmartContext.Build(ctx.FavoriteIds);   // recharge les resultats d'analyse
                    Refresh();
                }
            }
        };

        void UpdateValueBoxes(Row r)
        {
            bool isVibe = r.Rule.Field == SmartField.Vibe;
            if (isVibe)
            {
                int vi = Array.FindIndex(MoodAnalysisService.VibeKeys, k => string.Equals(k, r.Rule.Value, StringComparison.OrdinalIgnoreCase));
                if (vi < 0) { vi = 0; r.Rule.Value = MoodAnalysisService.VibeKeys[0]; }
                r.Building = true;
                r.VibeBox.SelectedIndex = vi;
                r.Building = false;
            }
            r.VibeBox.Visibility = isVibe ? Visibility.Visible : Visibility.Collapsed;
            int count = isVibe ? 0 : SmartPlaylistEngine.ValueCount(r.Rule.Op);
            bool isDate = r.Rule.Field == SmartField.DateAdded || r.Rule.Field == SmartField.LastPlayed;
            bool days = r.Rule.Op == SmartOp.InLastDays || r.Rule.Op == SmartOp.NotInLastDays;
            r.V1.Visibility = count >= 1 ? Visibility.Visible : Visibility.Collapsed;
            r.V2.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            string ph = days ? (fr ? "nombre de jours" : "number of days")
                      : isDate ? (fr ? "AAAA-MM-JJ" : "YYYY-MM-DD")
                      : r.Rule.Field is SmartField.Year or SmartField.TrackNumber or SmartField.DurationSeconds or SmartField.PlayCount or SmartField.Bpm or SmartField.Energy ? "0"
                      : (fr ? "valeur" : "value");
            r.V1.PlaceholderText = ph;
            r.V2.PlaceholderText = ph;
        }

        void BuildOps(Row r)
        {
            r.Building = true;
            r.Ops = SmartPlaylistEngine.OpsFor(r.Rule.Field);
            r.OpBox.Items.Clear();
            foreach (var op in r.Ops) r.OpBox.Items.Add(SmartPlaylistEngine.OpLabel(op, r.Rule.Field, fr));
            int idx = Array.IndexOf(r.Ops, r.Rule.Op);
            if (idx < 0) { idx = 0; r.Rule.Op = r.Ops[0]; }
            r.OpBox.SelectedIndex = idx;
            r.Building = false;
            UpdateValueBoxes(r);
        }

        void AddRow(SmartRule rule)
        {
            var r = new Row { Rule = new SmartRule { Field = rule.Field, Op = rule.Op, Value = rule.Value ?? "", Value2 = rule.Value2 ?? "" } };

            r.FieldBox.Width = 180;
            foreach (var f in AllFields) r.FieldBox.Items.Add(SmartPlaylistEngine.FieldLabel(f, fr));
            r.FieldBox.SelectedIndex = Array.IndexOf(AllFields, r.Rule.Field);

            r.OpBox.Width = 210;
            r.V1.Text = r.Rule.Value;
            r.V2.Text = r.Rule.Value2;
            r.V1.MinWidth = 110; r.V2.MinWidth = 110;
            r.V1.HorizontalAlignment = HorizontalAlignment.Stretch;
            r.V2.HorizontalAlignment = HorizontalAlignment.Stretch;
            r.VibeBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            foreach (string vk in MoodAnalysisService.VibeKeys) r.VibeBox.Items.Add(MoodAnalysisService.VibeLabel(vk, fr));

            var del = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 } };
            ToolTipService.SetToolTip(del, fr ? "Supprimer la règle" : "Remove rule");

            r.Panel.ColumnSpacing = 8;
            for (int i = 0; i < 5; i++) r.Panel.ColumnDefinitions.Add(new ColumnDefinition());
            r.Panel.ColumnDefinitions[0].Width = GridLength.Auto;
            r.Panel.ColumnDefinitions[1].Width = GridLength.Auto;
            r.Panel.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            r.Panel.ColumnDefinitions[3].Width = new GridLength(1, GridUnitType.Star);
            r.Panel.ColumnDefinitions[4].Width = GridLength.Auto;
            Grid.SetColumn(r.FieldBox, 0); Grid.SetColumn(r.OpBox, 1);
            Grid.SetColumn(r.V1, 2); Grid.SetColumn(r.V2, 3); Grid.SetColumn(del, 4);
            Grid.SetColumn(r.VibeBox, 2);
            r.Panel.Children.Add(r.FieldBox); r.Panel.Children.Add(r.OpBox);
            r.Panel.Children.Add(r.V1); r.Panel.Children.Add(r.V2); r.Panel.Children.Add(r.VibeBox); r.Panel.Children.Add(del);

            r.FieldBox.SelectionChanged += (_, _) =>
            {
                if (r.FieldBox.SelectedIndex < 0) return;
                r.Rule.Field = AllFields[r.FieldBox.SelectedIndex];
                BuildOps(r);
                Refresh();
            };
            r.OpBox.SelectionChanged += (_, _) =>
            {
                if (r.Building || r.OpBox.SelectedIndex < 0 || r.OpBox.SelectedIndex >= r.Ops.Length) return;
                r.Rule.Op = r.Ops[r.OpBox.SelectedIndex];
                UpdateValueBoxes(r);
                Refresh();
            };
            r.V1.TextChanged += (_, _) => { r.Rule.Value = r.V1.Text; Refresh(); };
            r.V2.TextChanged += (_, _) => { r.Rule.Value2 = r.V2.Text; Refresh(); };
            r.VibeBox.SelectionChanged += (_, _) =>
            {
                if (r.Building || r.VibeBox.SelectedIndex < 0) return;
                r.Rule.Value = MoodAnalysisService.VibeKeys[r.VibeBox.SelectedIndex];
                Refresh();
            };
            del.Click += (_, _) => { rows.Remove(r); rulesPanel.Children.Remove(r.Panel); Refresh(); };

            rows.Add(r);
            rulesPanel.Children.Add(r.Panel);
            BuildOps(r);
        }

        void LoadRules(SmartPlaylistDefinition d)
        {
            rows.Clear();
            rulesPanel.Children.Clear();
            foreach (var rule in d.Rules) AddRow(rule);
            matchBox.SelectedIndex = d.MatchAll ? 0 : 1;
            int si = Array.FindIndex(SmartPlaylistEngine.SortOptions, s => s.Key == d.Sort);
            sortBox.SelectedIndex = si < 0 ? 0 : si;
            limitBox.Value = Math.Max(0, d.Limit);
            Refresh();
        }

        // ------------------------------------------------------------ evenements
        addRuleBtn.Click += (_, _) => { AddRow(new SmartRule { Field = SmartField.Genre, Op = SmartOp.Is }); Refresh(); };
        nameBox.TextChanged += (_, _) => Refresh();
        matchBox.SelectionChanged += (_, _) => Refresh();
        sortBox.SelectionChanged += (_, _) => Refresh();
        limitBox.ValueChanged += (_, _) => Refresh();
        presetBox.SelectionChanged += (_, _) =>
        {
            int i = presetBox.SelectedIndex - 1;
            if (i < 0 || i >= presets.Count) return;
            var d = presets[i].Def;
            LoadRules(new SmartPlaylistDefinition
            {
                MatchAll = d.MatchAll, Sort = d.Sort, Limit = d.Limit,
                Rules = d.Rules.Select(x => new SmartRule { Field = x.Field, Op = x.Op, Value = x.Value, Value2 = x.Value2 }).ToList(),
            });
            if (initial == null && (string.IsNullOrWhiteSpace(nameBox.Text) || presets.Any(p => p.Fr == nameBox.Text || p.En == nameBox.Text) || nameBox.Text == (fr ? "Ma playlist intelligente" : "My smart playlist")))
                nameBox.Text = fr ? presets[i].Fr : presets[i].En;
        };

        foreach (var rule in def.Rules) AddRow(rule);
        if (def.Rules.Count == 0 && initial == null) AddRow(new SmartRule { Field = SmartField.Genre, Op = SmartOp.Is });

        // ------------------------------------------------------------ mise en page
        var root = new StackPanel { Spacing = 12 };
        root.Children.Add(nameBox);
        if (initial == null) root.Children.Add(presetBox);
        root.Children.Add(matchBox);
        root.Children.Add(new TextBlock { Text = fr ? "Règles" : "Rules", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(rulesPanel);
        root.Children.Add(addRuleBtn);
        var sortRow = new Grid { ColumnSpacing = 12 };
        sortRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sortRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(limitBox, 1);
        sortRow.Children.Add(sortBox); sortRow.Children.Add(limitBox);
        root.Children.Add(sortRow);
        // (le compteur et le panneau d'analyse sont affiches hors du ScrollViewer, en pied de fenetre)
        

        // Contenu a largeur fixe ; les regles defilent, mais le compteur et le bouton "Analyser la bibliotheque" restent toujours visibles.
        root.Width = 688;
        var scroll = new ScrollViewer
        {
            Content = root,
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 12, 0),
        };
        var layout = new StackPanel { Spacing = 12, Width = 700 };
        layout.Children.Add(scroll);
        layout.Children.Add(preview);
        layout.Children.Add(moodPanel);
        dialog.Content = layout;
        Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        var final = Collect();
        if (final.Rules.Count == 0) return null;
        return new Result(nameBox.Text.Trim(), final);
    }
}
