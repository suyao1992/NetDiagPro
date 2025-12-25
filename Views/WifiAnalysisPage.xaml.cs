using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class WifiAnalysisPage : Page
{
    private readonly WifiAnalyzerService _analyzer = new();

    public WifiAnalysisPage()
    {
        this.InitializeComponent();
        _ = LoadCurrentConnectionAsync();
    }

    private async Task LoadCurrentConnectionAsync()
    {
        try
        {
            var connection = await _analyzer.GetCurrentConnectionAsync();
            
            if (connection != null)
            {
                CurrentSSID.Text = string.IsNullOrEmpty(connection.SSID) ? "未连接" : connection.SSID;
                CurrentChannel.Text = connection.Channel > 0 ? $"Ch {connection.Channel}" : "--";
                CurrentSignal.Text = connection.Signal > 0 ? $"{connection.Signal}%" : "--";
                CurrentBand.Text = connection.Channel switch
                {
                    >= 1 and <= 13 => "2.4 GHz",
                    > 30 => "5 GHz",
                    _ => "--"
                };
                ReceiveRate.Text = connection.ReceiveRate;
                TransmitRate.Text = connection.TransmitRate;
                Authentication.Text = connection.Authentication;
            }
            else
            {
                CurrentSSID.Text = "未连接 Wi-Fi";
            }
        }
        catch
        {
            CurrentSSID.Text = "获取失败";
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        ScanProgress.IsActive = true;
        ScanProgress.Visibility = Visibility.Visible;

        try
        {
            // Scan networks
            var networks = await _analyzer.ScanNetworksAsync();
            NetworksList.ItemsSource = networks;

            // Analyze channels
            var analysis = await _analyzer.AnalyzeChannelsAsync();
            
            TotalNetworks.Text = analysis.TotalNetworks.ToString();
            Best24Channel.Text = analysis.Best24Channel.ToString();
            
            CongestionLevel.Text = analysis.CongestionLevel;
            CongestionLevel.Foreground = analysis.CongestionLevel switch
            {
                "低" => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129)),
                "中" => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 245, 158, 11)),
                _ => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68))
            };

            if (!string.IsNullOrEmpty(analysis.Recommendation))
            {
                RecommendationBar.Message = analysis.Recommendation;
                RecommendationBar.Severity = analysis.CongestionLevel switch
                {
                    "低" => InfoBarSeverity.Success,
                    "中" => InfoBarSeverity.Warning,
                    _ => InfoBarSeverity.Error
                };
                RecommendationBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            RecommendationBar.Severity = InfoBarSeverity.Error;
            RecommendationBar.Message = $"扫描失败: {ex.Message}";
            RecommendationBar.IsOpen = true;
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            ScanProgress.IsActive = false;
            ScanProgress.Visibility = Visibility.Collapsed;
        }
    }
}
