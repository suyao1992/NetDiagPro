using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NetDiagPro.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Set default selections
        ThemeCombo.SelectedIndex = 0;
        LanguageCombo.SelectedIndex = 0;
    }

    private void ThemeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            var requestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            // Apply theme to root element
            if (App.MainWindow?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = requestedTheme;
            }
        }
    }

    private void LanguageCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            // Language change would require restart or resource reload
            // For now, just save the preference
            Windows.Storage.ApplicationData.Current.LocalSettings.Values["Language"] = lang;
        }
    }
}
