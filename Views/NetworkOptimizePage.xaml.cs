using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class NetworkOptimizePage : Page
{
    private readonly NetworkOptimizerService _optimizer = new();
    private DNSTestResult? _bestDns;

    public NetworkOptimizePage()
    {
        this.InitializeComponent();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            CurrentDnsText.Text = $"当前 DNS: {await _optimizer.GetCurrentDNSAsync()}";
            await EvaluateHealthAsync();
        }
        catch { }
    }

    private async void Evaluate_Click(object sender, RoutedEventArgs e)
    {
        await EvaluateHealthAsync();
    }

    private async Task EvaluateHealthAsync()
    {
        EvaluateBtn.IsEnabled = false;
        GradeText.Text = "评估中...";

        try
        {
            var report = await _optimizer.EvaluateHealthAsync(status =>
            {
                DispatcherQueue.TryEnqueue(() => GradeText.Text = status);
            });

            ScoreText.Text = report.OverallScore.ToString();
            GradeText.Text = $"等级: {report.Grade}";

            DnsStatusText.Text = $"DNS: {(report.DnsLatency > 0 ? $"{report.DnsLatency:F0}ms" : "失败")} " +
                $"{(report.DnsLatency < 50 ? "✅" : report.DnsLatency < 100 ? "⚠️" : "❌")}";
            GatewayStatusText.Text = $"网关: {(report.GatewayOk ? "正常 ✅" : "异常 ❌")}";
            InternetStatusText.Text = $"外网: {(report.InternetOk ? "正常 ✅" : "异常 ❌")}";
            PacketLossText.Text = $"丢包: {report.PacketLoss:F0}% {(report.PacketLoss < 5 ? "✅" : "❌")}";
        }
        catch (Exception ex)
        {
            GradeText.Text = $"评估失败: {ex.Message}";
        }
        finally
        {
            EvaluateBtn.IsEnabled = true;
        }
    }

    private async void TestDNS_Click(object sender, RoutedEventArgs e)
    {
        TestDnsBtn.IsEnabled = false;
        DnsResultsList.Items.Clear();

        try
        {
            var results = await _optimizer.TestAllDNSAsync(status =>
            {
                DispatcherQueue.TryEnqueue(() => RecommendDnsText.Text = status);
            });

            _bestDns = results.FirstOrDefault(r => r.Available);

            foreach (var result in results)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var nameText = new TextBlock { Text = result.Server.Name };
                Grid.SetColumn(nameText, 0);

                var latencyText = new TextBlock
                {
                    Text = result.Available ? $"{result.LatencyMs:F0} ms" : "超时",
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        result.Available && result.LatencyMs < 50 ? Microsoft.UI.Colors.Green
                            : result.Available ? Microsoft.UI.Colors.Orange
                            : Microsoft.UI.Colors.Red)
                };
                Grid.SetColumn(latencyText, 1);

                var ipText = new TextBlock
                {
                    Text = result.Server.Primary,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };
                Grid.SetColumn(ipText, 2);

                grid.Children.Add(nameText);
                grid.Children.Add(latencyText);
                grid.Children.Add(ipText);

                DnsResultsList.Items.Add(grid);
            }

            if (_bestDns != null)
            {
                RecommendDnsText.Text = $"推荐 DNS: {_bestDns.Server.Name} ({_bestDns.Server.Primary}) - {_bestDns.LatencyMs:F0}ms";
                ApplyDnsBtn.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            RecommendDnsText.Text = $"测试失败: {ex.Message}";
        }
        finally
        {
            TestDnsBtn.IsEnabled = true;
        }
    }

    private async void ApplyDNS_Click(object sender, RoutedEventArgs e)
    {
        if (_bestDns == null) return;

        var dialog = new ContentDialog
        {
            Title = "应用 DNS",
            Content = $"确定要将 DNS 更改为 {_bestDns.Server.Name} ({_bestDns.Server.Primary}) 吗？\n\n此操作需要管理员权限。",
            PrimaryButtonText = "应用",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            FixStatusText.Text = "DNS 更改需要在系统网络设置中手动配置，或使用管理员权限执行 netsh 命令。";
        }
    }

    private async void FlushDNS_Click(object sender, RoutedEventArgs e)
    {
        FlushDnsBtn.IsEnabled = false;
        FixStatusText.Text = "正在清除 DNS 缓存...";

        var result = await _optimizer.FlushDNSAsync();
        FixStatusText.Text = result.Success ? "✅ DNS 缓存已清除" : $"❌ 失败: {result.Error}";

        FlushDnsBtn.IsEnabled = true;
    }

    private async void ReleaseRenew_Click(object sender, RoutedEventArgs e)
    {
        ReleaseRenewBtn.IsEnabled = false;
        FixStatusText.Text = "正在释放 IP...";

        await _optimizer.ReleaseIPAsync();
        FixStatusText.Text = "正在续订 IP...";

        var result = await _optimizer.RenewIPAsync();
        FixStatusText.Text = result.Success ? "✅ IP 已续订" : $"⚠️ 续订完成 (可能需要等待)";

        ReleaseRenewBtn.IsEnabled = true;
    }

    private async void ResetWinsock_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "重置 Winsock",
            Content = "此操作需要管理员权限，重置后建议重启计算机。确定继续吗？",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Primary)
        {
            FixStatusText.Text = "正在重置 Winsock...";
            var result = await _optimizer.ResetWinsockAsync();
            FixStatusText.Text = result.Success ? "✅ Winsock 已重置，建议重启计算机" : $"❌ 失败: {result.Error}";
        }
    }

    private async void ResetTCP_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "重置 TCP/IP",
            Content = "此操作需要管理员权限，重置后需要重启计算机。确定继续吗？",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Primary)
        {
            FixStatusText.Text = "正在重置 TCP/IP...";
            var result = await _optimizer.ResetTCPIPAsync();
            FixStatusText.Text = result.Success ? "✅ TCP/IP 已重置，请重启计算机" : $"❌ 失败: {result.Error}";
        }
    }

    private async void DetectMTU_Click(object sender, RoutedEventArgs e)
    {
        DetectMtuBtn.IsEnabled = false;
        OptimalMtuText.Text = "正在检测最佳 MTU...";

        try
        {
            var optimalMtu = await _optimizer.DetectOptimalMTUAsync();
            OptimalMtuText.Text = $"最佳 MTU: {optimalMtu}";

            if (optimalMtu < 1500)
            {
                MtuInfoBar.IsOpen = true;
                MtuInfoBar.Message = $"检测到最佳 MTU 为 {optimalMtu}，低于默认值 1500。这可能是由于 VPN 或 PPPoE 连接导致。";
            }
        }
        catch (Exception ex)
        {
            OptimalMtuText.Text = $"检测失败: {ex.Message}";
        }
        finally
        {
            DetectMtuBtn.IsEnabled = true;
        }
    }
}
