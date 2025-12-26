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
            var status = await _analyzer.CheckWifiStatusAsync();
            
            switch (status.Availability)
            {
                case WifiAvailability.Available:
                    if (status.Connection != null)
                    {
                        CurrentSSID.Text = string.IsNullOrEmpty(status.Connection.SSID) ? "未连接" : status.Connection.SSID;
                        CurrentChannel.Text = status.Connection.Channel > 0 ? $"Ch {status.Connection.Channel}" : "--";
                        CurrentSignal.Text = status.Connection.Signal > 0 ? $"{status.Connection.Signal}%" : "--";
                        CurrentBand.Text = status.Connection.Channel switch
                        {
                            >= 1 and <= 13 => "2.4 GHz",
                            > 30 => "5 GHz",
                            _ => "--"
                        };
                        ReceiveRate.Text = string.IsNullOrEmpty(status.Connection.ReceiveRate) ? "--" : status.Connection.ReceiveRate;
                        TransmitRate.Text = string.IsNullOrEmpty(status.Connection.TransmitRate) ? "--" : status.Connection.TransmitRate;
                        Authentication.Text = string.IsNullOrEmpty(status.Connection.Authentication) ? "--" : status.Connection.Authentication;
                    }
                    else
                    {
                        CurrentSSID.Text = "Wi-Fi 未连接";
                        ShowConnectionMessage("Wi-Fi 可用但未连接到任何网络", InfoBarSeverity.Warning);
                    }
                    break;
                    
                case WifiAvailability.ServiceNotRunning:
                    CurrentSSID.Text = "服务未运行";
                    ShowConnectionMessage("WLAN AutoConfig 服务未运行\n请打开服务管理器 (services.msc) 并启动 'WLAN AutoConfig' 服务", InfoBarSeverity.Error);
                    break;
                    
                case WifiAvailability.NoAdapter:
                    CurrentSSID.Text = "无无线网卡";
                    ShowConnectionMessage("未检测到无线网卡\n此设备可能不支持 Wi-Fi 或无线适配器已禁用", InfoBarSeverity.Warning);
                    break;
                    
                default:
                    CurrentSSID.Text = "状态未知";
                    ShowConnectionMessage(status.Message, InfoBarSeverity.Error);
                    break;
            }
        }
        catch (Exception ex)
        {
            CurrentSSID.Text = "获取失败";
            ShowConnectionMessage($"检查 WiFi 状态时出错: {ex.Message}", InfoBarSeverity.Error);
        }
    }
    
    private void ShowConnectionMessage(string message, InfoBarSeverity severity)
    {
        RecommendationBar.Title = severity == InfoBarSeverity.Error ? "错误" : "提示";
        RecommendationBar.Message = message;
        RecommendationBar.Severity = severity;
        RecommendationBar.IsOpen = true;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        ScanProgress.IsActive = true;
        ScanProgress.Visibility = Visibility.Visible;
        RecommendationBar.IsOpen = false;

        try
        {
            // 先检查 WiFi 状态
            var status = await _analyzer.CheckWifiStatusAsync();
            
            if (status.Availability != WifiAvailability.Available)
            {
                RecommendationBar.Severity = InfoBarSeverity.Error;
                RecommendationBar.Title = "无法扫描";
                RecommendationBar.Message = status.Message;
                RecommendationBar.IsOpen = true;
                return;
            }
            
            // Scan networks
            var (networks, scanError) = await _analyzer.ScanNetworksAsync();
            
            if (scanError != null)
            {
                RecommendationBar.Severity = InfoBarSeverity.Error;
                RecommendationBar.Title = "扫描失败";
                RecommendationBar.Message = scanError;
                RecommendationBar.IsOpen = true;
                return;
            }
            
            NetworksList.ItemsSource = networks;

            // Analyze channels
            var analysis = await _analyzer.AnalyzeChannelsAsync();
            
            if (analysis.Error != null)
            {
                RecommendationBar.Severity = InfoBarSeverity.Error;
                RecommendationBar.Title = "分析失败";
                RecommendationBar.Message = analysis.Error;
                RecommendationBar.IsOpen = true;
                return;
            }
            
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
                RecommendationBar.Title = "优化建议";
                RecommendationBar.Message = analysis.Recommendation;
                RecommendationBar.Severity = analysis.CongestionLevel switch
                {
                    "低" => InfoBarSeverity.Success,
                    "中" => InfoBarSeverity.Warning,
                    _ => InfoBarSeverity.Error
                };
                RecommendationBar.IsOpen = true;
            }
            else if (analysis.TotalNetworks == 0)
            {
                RecommendationBar.Title = "扫描完成";
                RecommendationBar.Message = "未发现周围 Wi-Fi 网络";
                RecommendationBar.Severity = InfoBarSeverity.Informational;
                RecommendationBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            RecommendationBar.Severity = InfoBarSeverity.Error;
            RecommendationBar.Title = "扫描失败";
            RecommendationBar.Message = $"发生错误: {ex.Message}";
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
