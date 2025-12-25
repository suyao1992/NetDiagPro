using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class SpeedTestPage : Page
{
    private readonly SpeedTestService _speedService = new();
    private readonly DatabaseService _db = new();
    private bool _isTesting = false;

    public SpeedTestPage()
    {
        this.InitializeComponent();
    }

    private async void StartTest_Click(object sender, RoutedEventArgs e)
    {
        if (_isTesting) return;
        
        _isTesting = true;
        StartTestBtn.IsEnabled = false;
        TestProgress.IsActive = true;
        TestButtonText.Text = "测试中...";

        try
        {
            // Ping test
            StatusText.Text = "正在测试延迟...";
            var ping = await _speedService.MeasurePingAsync("www.google.com");
            PingText.Text = ping > 0 ? $"{ping:F0}" : "--";

            // Download test
            StatusText.Text = "正在测试下载速度...";
            var downloadSpeed = await _speedService.MeasureDownloadSpeedAsync(
                (speed) => DownloadSpeedText.Text = $"{speed:F1}");
            DownloadSpeedText.Text = $"{downloadSpeed:F1}";

            // Upload test
            StatusText.Text = "正在测试上传速度...";
            var uploadSpeed = await _speedService.MeasureUploadSpeedAsync(
                (speed) => UploadSpeedText.Text = $"{speed:F1}");
            UploadSpeedText.Text = $"{uploadSpeed:F1}";

            // Save to history database
            await _db.SaveRecordAsync(
                target: "SpeedTest",
                latencyMs: ping,
                dnsMs: 0,
                tcpMs: 0,
                statusCode: 200,
                success: true
            );

            StatusText.Text = "测速完成";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"测速失败: {ex.Message}";
        }
        finally
        {
            _isTesting = false;
            StartTestBtn.IsEnabled = true;
            TestProgress.IsActive = false;
            TestButtonText.Text = "开始测速";
        }
    }

    private async void TestStreaming_Click(object sender, RoutedEventArgs e)
    {
        TestStreamingBtn.IsEnabled = false;
        StreamingResults.Items.Clear();

        var platforms = new[]
        {
            ("YouTube", "https://www.youtube.com", "🎬"),
            ("Netflix", "https://www.netflix.com", "🎥"),
            ("Disney+", "https://www.disneyplus.com", "🏰"),
            ("Twitch", "https://www.twitch.tv", "🎮"),
            ("Bilibili", "https://www.bilibili.com", "📺")
        };

        foreach (var (name, url, icon) in platforms)
        {
            try
            {
                var (available, latency) = await _speedService.TestPlatformAsync(url);
                AddStreamingResult(name, icon, available, latency);
            }
            catch
            {
                AddStreamingResult(name, icon, false, 0);
            }
        }

        TestStreamingBtn.IsEnabled = true;
    }

    private void AddStreamingResult(string name, string icon, bool available, double latency)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var iconText = new TextBlock { Text = icon, FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(iconText, 0);

        var nameText = new TextBlock 
        { 
            Text = name, 
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameText, 1);

        var statusText = new TextBlock
        {
            Text = available ? $"{latency:F0}ms" : "不可用",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                available ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusText, 2);

        grid.Children.Add(iconText);
        grid.Children.Add(nameText);
        grid.Children.Add(statusText);

        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Child = grid
        };

        StreamingResults.Items.Add(border);
    }
}
