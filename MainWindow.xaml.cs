using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Views;

namespace NetDiagPro;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        // Set Mica backdrop for Windows 11 look
        try
        {
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        }
        catch { }
        
        // Set initial page
        ContentFrame.Navigate(typeof(DashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "LiveMonitor":
                    ContentFrame.Navigate(typeof(LiveMonitorPage));
                    break;
                case "SpeedTest":
                    ContentFrame.Navigate(typeof(SpeedTestPage));
                    break;
                case "Traceroute":
                    ContentFrame.Navigate(typeof(TraceroutePage));
                    break;
                case "LANDiagnostics":
                    ContentFrame.Navigate(typeof(LANDiagnosticsPage));
                    break;
                case "NetworkOptimize":
                    ContentFrame.Navigate(typeof(NetworkOptimizePage));
                    break;
                case "AIAssistant":
                    ContentFrame.Navigate(typeof(AIAssistantPage));
                    break;
                case "TrafficMonitor":
                    ContentFrame.Navigate(typeof(TrafficMonitorPage));
                    break;
                case "WifiAnalysis":
                    ContentFrame.Navigate(typeof(WifiAnalysisPage));
                    break;
                case "History":
                    ContentFrame.Navigate(typeof(HistoryPage));
                    break;
            }
        }
    }
}
