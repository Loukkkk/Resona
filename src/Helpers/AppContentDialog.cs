using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Resona.Helpers;

/// <summary>
/// ContentDialog dont l'apparence ne depend JAMAIS du theme de l'app (texte blanc, controles sombres).
/// Remplace Microsoft.UI.Xaml.Controls.ContentDialog dans tout le projet via l'alias global
/// declare dans GlobalUsings.cs. Les brushes de theme (mutes par App.ApplyThemeResources)
/// sont surcharges localement, au niveau du dialogue uniquement.
/// </summary>
public sealed class AppContentDialog : Microsoft.UI.Xaml.Controls.ContentDialog
{
    /// <summary>Centre le bouton de fermeture (utile quand il est seul : sinon il se colle a droite).</summary>
    public bool CenterCloseButton { get; set; }

    public AppContentDialog()
    {
        Opened += (_, _) => { if (CenterCloseButton) CenterCloseButtonNow(); };
        DefaultStyleKey = typeof(Microsoft.UI.Xaml.Controls.ContentDialog);
        // Le style implicite global (titre centre) est lie au type exact ContentDialog : on le reapplique.
        if (Application.Current.Resources.TryGetValue(typeof(Microsoft.UI.Xaml.Controls.ContentDialog), out object? s) && s is Style style)
            Style = style;

        RequestedTheme = ElementTheme.Dark;

        Color white = Color.FromArgb(255, 255, 255, 255);
        Color white80 = Color.FromArgb(200, 255, 255, 255);
        Color white60 = Color.FromArgb(160, 255, 255, 255);
        Color white36 = Color.FromArgb(92, 255, 255, 255);
        Color fill = Color.FromArgb(24, 255, 255, 255);
        Color fillHover = Color.FromArgb(40, 255, 255, 255);
        Color fillPressed = Color.FromArgb(15, 255, 255, 255);
        Color stroke = Color.FromArgb(0x28, 255, 255, 255);
        Color strokeStrong = Color.FromArgb(0x99, 255, 255, 255);

        void Set(string key, Color c) => Resources[key] = new SolidColorBrush(c);

        // Texte
        Set("TextFillColorPrimaryBrush", white);
        Set("TextFillColorSecondaryBrush", white80);
        Set("TextFillColorTertiaryBrush", white60);
        Set("TextFillColorDisabledBrush", white36);
        Set("ContentDialogForeground", white);

        // Fonds / bordures generiques
        Set("ControlFillColorDefaultBrush", fill);
        Set("ControlFillColorSecondaryBrush", fill);
        Set("ControlFillColorTertiaryBrush", Color.FromArgb(8, 255, 255, 255));
        Set("ControlStrongStrokeColorDefaultBrush", strokeStrong);
        Set("CardStrokeColorDefaultBrush", stroke);
        Set("DividerStrokeColorDefaultBrush", stroke);

        // Boutons
        foreach (var k in new[] { "ButtonForeground", "ButtonForegroundPointerOver", "ButtonForegroundPressed" }) Set(k, white);
        Set("ButtonForegroundDisabled", white36);
        Set("ButtonBackground", fill);
        Set("ButtonBackgroundPointerOver", fillHover);
        Set("ButtonBackgroundPressed", fillPressed);
        Set("ButtonBorderBrush", stroke);
        Set("ButtonBorderBrushPointerOver", stroke);
        Set("ButtonBorderBrushPressed", stroke);

        // CheckBox / RadioButton / ToggleSwitch
        foreach (var k in new[] { "CheckBoxForeground", "CheckBoxForegroundUnchecked", "CheckBoxForegroundUncheckedPointerOver", "CheckBoxForegroundUncheckedPressed",
                                  "CheckBoxForegroundChecked", "CheckBoxForegroundCheckedPointerOver", "CheckBoxForegroundCheckedPressed",
                                  "CheckBoxForegroundIndeterminate", "CheckBoxForegroundIndeterminatePointerOver", "CheckBoxForegroundIndeterminatePressed",
                                  "CheckBoxForegroundBrush",
                                  "RadioButtonForeground", "RadioButtonForegroundPointerOver", "RadioButtonForegroundPressed", "RadioButtonForegroundBrush",
                                  "ToggleSwitchContentForeground", "ToggleSwitchHeaderForeground", "ToggleSwitchForegroundBrush" })
            Set(k, white);
        foreach (var k in new[] { "CheckBoxCheckBackgroundStrokeUnchecked", "CheckBoxCheckBackgroundStrokeUncheckedPointerOver", "CheckBoxCheckBackgroundStrokeUncheckedPressed",
                                  "RadioButtonOuterEllipseStroke", "RadioButtonOuterEllipseStrokePointerOver", "RadioButtonOuterEllipseStrokePressed",
                                  "ToggleSwitchStrokeOff", "ToggleSwitchStrokeOffPointerOver", "ToggleSwitchStrokeOffPressed" })
            Set(k, strokeStrong);

        // Champs texte : fond sombre translucide, texte blanc
        foreach (var k in new[] { "TextBoxForeground", "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused",
                                  "TextBoxForegroundBrush", "TextBoxForegroundPointerOver", "TextBoxForegroundPressed", "TextBoxForegroundSelected" })
            Set(k, white);
        Set("TextControlForegroundDisabled", white60);
        Set("TextBoxForegroundDisabled", white60);
        Set("TextControlPlaceholderForeground", white60);
        Set("TextControlPlaceholderForegroundPointerOver", white60);
        Set("TextControlPlaceholderForegroundFocused", white36);
        foreach (var k in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
                                  "TextBoxBackground", "TextBoxBackgroundPointerOver", "TextBoxBackgroundFocused" })
            Set(k, fill);
        foreach (var k in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextBoxBorderBrush", "TextBoxBorderBrushPointerOver", "TextBoxBorderBrushFocused" })
            Set(k, stroke);

        // Listes deroulantes dans les dialogues
        foreach (var k in new[] { "ComboBoxForeground", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed", "ComboBoxForegroundDisabled", "ComboBoxForegroundSelected",
                                  "ComboBoxForegroundBrush", "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundPointerOver", "ComboBoxDropDownGlyphForegroundPressed",
                                  "ComboBoxItemForeground", "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundPressed", "ComboBoxItemForegroundSelected",
                                  "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxItemForegroundSelectedPressed" })
            Set(k, white);
        Set("ComboBoxBackground", fill);
        Set("ComboBoxBackgroundPointerOver", fillHover);
        Set("ComboBoxBackgroundPressed", fillPressed);
        Set("ComboBoxBorderBrush", stroke);

        // Bouton principal (accent) : en theme blanc pur l'accent est blanc, donc au survol/pression le fond
        // devenait blanc avec un texte blanc (illisible). On fixe ici des couleurs explicites et contrastees :
        // fond clair + texte sombre, qui s'assombrit legerement au survol.
        if (Resona.App.IsPureWhiteTheme)
        {
            Color accRest = Color.FromArgb(255, 0xF2, 0xF2, 0xF2);
            Color accHover = Color.FromArgb(255, 0xD9, 0xD9, 0xD9);
            Color accPressed = Color.FromArgb(255, 0xBF, 0xBF, 0xBF);
            Color accFg = Color.FromArgb(255, 0x1A, 0x1A, 0x1A);
            Color accFgDisabled = Color.FromArgb(0x66, 255, 255, 255);
            Set("AccentButtonBackground", accRest);
            Set("AccentButtonBackgroundPointerOver", accHover);
            Set("AccentButtonBackgroundPressed", accPressed);
            Set("AccentButtonBackgroundDisabled", fill);
            Set("AccentButtonBorderBrush", accRest);
            Set("AccentButtonBorderBrushPointerOver", accHover);
            Set("AccentButtonBorderBrushPressed", accPressed);
            Set("AccentButtonBorderBrushDisabled", fill);
            Set("AccentButtonForeground", accFg);
            Set("AccentButtonForegroundPointerOver", accFg);
            Set("AccentButtonForegroundPressed", accFg);
            Set("AccentButtonForegroundDisabled", accFgDisabled);
        }
    }

    private void CenterCloseButtonNow()
    {
        var close = FindByName(this, "CloseButton") as Button;
        if (close == null || VisualTreeHelper.GetParent(close) is not Grid grid) return;

        // Le bouton seul occupe une colonne a droite : on ne garde que sa colonne (toute la largeur)
        // puis on le centre avec une largeur fixe.
        int col = Grid.GetColumn(close);
        for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
            grid.ColumnDefinitions[i].Width = i == col ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        close.HorizontalAlignment = HorizontalAlignment.Center;
        close.Width = 160;
    }

    private static DependencyObject? FindByName(DependencyObject root, string name)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name) return child;
            var found = FindByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
