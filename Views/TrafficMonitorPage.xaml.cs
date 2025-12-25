using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class TrafficMonitorPage : Page
{
    private readonly TrafficMonitorService _monitor = new();
    private DispatcherTimer? _timer;
    private NetworkInterfaceStats? _previousStats;
    private bool _isMonitoring;

    public TrafficMonitorPage()
    {
        this.InitializeComponent();
        _ = LoadProcessListAsync();
        UpdateInterfaceStats();
    }

    private void MonitorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isMonitoring)
        {
            StopMonitoring();
        }
        else
        {
            StartMonitoring();
        }
    }

    private void StartMonitoring()
    {
        _isMonitoring = true;
        MonitorIcon.Glyph = "\uE71A";
        MonitorText.Text = "停止监控";
        MonitorToggle.IsChecked = true;

        _previousStats = _monitor.GetInterfaceStats();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void StopMonitoring()
    {
        _isMonitoring = false;
        MonitorIcon.Glyph = "\uE768";
        MonitorText.Text = "开始监控";
        MonitorToggle.IsChecked = false;

        _timer?.Stop();
        _timer = null;
    }

    private void Timer_Tick(object? sender, object e)
    {
        var currentStats = _monitor.GetInterfaceStats();

        if (_previousStats != null)
        {
            var (download, upload) = _monitor.CalculateSpeed(_previousStats, currentStats);
            DownloadSpeedText.Text = download.ToString("F2");
            UploadSpeedText.Text = upload.ToString("F2");
        }

        UpdateInterfaceStatsUI(currentStats);
        _previousStats = currentStats;
        LastUpdateText.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
    }

    private void UpdateInterfaceStats()
    {
        var stats = _monitor.GetInterfaceStats();
        UpdateInterfaceStatsUI(stats);
    }

    private void UpdateInterfaceStatsUI(NetworkInterfaceStats stats)
    {
        InterfaceNameText.Text = $"接口: {stats.InterfaceName}";
        InterfaceSpeedText.Text = $"带宽: {stats.Speed / 1_000_000} Mbps";
        TotalReceivedText.Text = $"总接收: {TrafficMonitorService.FormatBytes(stats.BytesReceived)}";
        TotalSentText.Text = $"总发送: {TrafficMonitorService.FormatBytes(stats.BytesSent)}";
        PacketsReceivedText.Text = $"接收包: {stats.PacketsReceived:N0}";
        PacketsSentText.Text = $"发送包: {stats.PacketsSent:N0}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadProcessListAsync();
    }

    private async Task LoadProcessListAsync()
    {
        LoadingRing.IsActive = true;
        RefreshBtn.IsEnabled = false;
        ProcessList.Items.Clear();

        try
        {
            var processes = await _monitor.GetProcessNetworkUsageAsync();

            foreach (var proc in processes)
            {
                var grid = new Grid { Padding = new Thickness(8, 4, 8, 4) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                var nameText = new TextBlock { Text = proc.ProcessName };
                Grid.SetColumn(nameText, 0);

                var countText = new TextBlock
                {
                    Text = proc.ConnectionCount.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        proc.ConnectionCount > 10 ? Microsoft.UI.Colors.Orange : Microsoft.UI.Colors.Green)
                };
                Grid.SetColumn(countText, 1);

                grid.Children.Add(nameText);
                grid.Children.Add(countText);

                var border = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4),
                    Child = grid
                };

                ProcessList.Items.Add(border);
            }

            if (processes.Count == 0)
            {
                ProcessList.Items.Add(new TextBlock
                {
                    Text = "暂无活跃网络进程",
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }
        }
        catch (Exception ex)
        {
            ProcessList.Items.Add(new TextBlock { Text = $"加载失败: {ex.Message}" });
        }
        finally
        {
            LoadingRing.IsActive = false;
            RefreshBtn.IsEnabled = true;
        }
    }
}
