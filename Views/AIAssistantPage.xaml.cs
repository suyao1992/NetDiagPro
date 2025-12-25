using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class AIAssistantPage : Page
{
    private readonly NetworkTesterService _tester = new();
    private readonly SpeedTestService _speedTest = new();
    private readonly LANScannerService _scanner = new();
    private readonly IPDetectorService _ipDetector = new();
    private readonly NetworkOptimizerService _optimizer = new();
    private readonly ZepAIService _zepAI;

    // Zep AI API Key
    private const string ZepApiKey = "z_1dWlkIjoiM2QzYzc3OTAtMGJjMS00ZmY1LTg4MTAtM2RkZTUwY2FmMjc5In0.jek4gwbLwUxdvQ0OkL7L3nUsBrd1GpxdLmwrrVhlxW0bp3dFfIQCmXebu7RlLIjkVE5dFtzcdNniBTONzWbmXA";

    public AIAssistantPage()
    {
        this.InitializeComponent();
        _zepAI = new ZepAIService(ZepApiKey);
        _ = InitializeZepAsync();
        AddAIMessage("你好！我是 AI 网络助手 🤖\n\n我可以帮你：\n• 诊断网络问题\n• 测试网速和延迟\n• 扫描局域网设备\n• 优化 DNS 设置\n• 检测 IP 信息\n\n有什么可以帮到你的？");
    }

    private async Task InitializeZepAsync()
    {
        try
        {
            // 初始化 Zep 用户和线程
            var userId = $"netdiag_user_{Environment.MachineName}";
            await _zepAI.InitializeUserAsync(userId, "NetDiag User");
            await _zepAI.CreateThreadAsync();

            // 记录会话开始事件
            await _zepAI.RecordNetworkEventAsync(new NetworkEventData
            {
                EventType = "session_start",
                Timestamp = DateTime.Now,
                AdditionalData = new Dictionary<string, object>
                {
                    ["machine"] = Environment.MachineName,
                    ["os_version"] = Environment.OSVersion.ToString()
                }
            });
        }
        catch
        {
            // Zep 初始化失败不影响基本功能
        }
    }

    private void AddUserMessage(string message)
    {
        var border = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246)),
            CornerRadius = new CornerRadius(12, 12, 4, 12),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            MaxWidth = 500
        };

        border.Child = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
        };

        MessagesList.Items.Add(border);
        ScrollToBottom();

        // 记录到 Zep
        _ = _zepAI.AddMessageAsync("user", message);
    }

    private void AddAIMessage(string message)
    {
        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(12, 12, 12, 4),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 500
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = "🤖", FontSize = 16 });
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        border.Child = stack;
        MessagesList.Items.Add(border);
        ScrollToBottom();

        // 记录到 Zep
        _ = _zepAI.AddMessageAsync("assistant", message);
    }

    private void AddLoadingMessage()
    {
        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(12, 12, 12, 4),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Tag = "loading"
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        stack.Children.Add(new ProgressRing { Width = 16, Height = 16, IsActive = true });
        stack.Children.Add(new TextBlock { Text = "正在分析..." });

        border.Child = stack;
        MessagesList.Items.Add(border);
        ScrollToBottom();
    }

    private void RemoveLoadingMessage()
    {
        var toRemove = MessagesList.Items.Cast<Border>()
            .FirstOrDefault(b => b.Tag?.ToString() == "loading");
        if (toRemove != null)
        {
            MessagesList.Items.Remove(toRemove);
        }
    }

    private void ScrollToBottom()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
        });
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        await ProcessUserInput();
    }

    private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ProcessUserInput();
        }
    }

    private void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string action)
        {
            InputBox.Text = action;
            _ = ProcessUserInput();
        }
    }

    private async Task ProcessUserInput()
    {
        var input = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        AddUserMessage(input);
        InputBox.Text = "";
        SendBtn.IsEnabled = false;
        AddLoadingMessage();

        try
        {
            var response = await ProcessCommandAsync(input);
            RemoveLoadingMessage();
            AddAIMessage(response);
        }
        catch (Exception ex)
        {
            RemoveLoadingMessage();
            AddAIMessage($"抱歉，出现了错误：{ex.Message}");
        }
        finally
        {
            SendBtn.IsEnabled = true;
        }
    }

    private async Task<string> ProcessCommandAsync(string input)
    {
        var lower = input.ToLower();

        // 测速
        if (lower.Contains("网速") || lower.Contains("测速") || lower.Contains("speed"))
        {
            var result = await _speedTest.RunFullTestAsync();
            if (result.Success)
            {
                // 记录测速事件
                _ = _zepAI.RecordNetworkEventAsync(new NetworkEventData
                {
                    EventType = "speed_test",
                    DownloadMbps = result.DownloadMbps,
                    UploadMbps = result.UploadMbps,
                    PingMs = result.PingMs
                });

                return $"📊 测速结果:\n\n" +
                       $"⬇️ 下载: {result.DownloadMbps:F1} Mbps\n" +
                       $"⬆️ 上传: {result.UploadMbps:F1} Mbps\n" +
                       $"📶 延迟: {result.PingMs:F0} ms\n\n" +
                       GetSpeedAdvice(result.DownloadMbps, result.PingMs);
            }
            return "测速失败，请检查网络连接。";
        }

        // 扫描局域网
        if (lower.Contains("局域网") || lower.Contains("扫描") || lower.Contains("设备") || lower.Contains("lan"))
        {
            var devices = await _scanner.ScanNetworkAsync(false);
            var summary = $"📡 扫描完成！发现 {devices.Count} 台设备:\n\n";
            foreach (var d in devices.Take(10))
            {
                var icon = d.DeviceType == "gateway" ? "🌐" : d.DeviceType == "self" ? "💻" : "📱";
                summary += $"{icon} {d.IP} ({d.Vendor})\n";
            }
            if (devices.Count > 10)
            {
                summary += $"\n...还有 {devices.Count - 10} 台设备";
            }
            return summary;
        }

        // DNS 优化
        if (lower.Contains("dns") || lower.Contains("优化"))
        {
            var currentDns = await _optimizer.GetCurrentDNSAsync();
            var dnsResults = await _optimizer.TestAllDNSAsync();
            var best = dnsResults.FirstOrDefault(r => r.Available);

            if (best != null)
            {
                // 记录 DNS 优化事件
                _ = _zepAI.RecordNetworkEventAsync(new NetworkEventData
                {
                    EventType = "dns_optimization",
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["current_dns"] = currentDns,
                        ["recommended_dns"] = best.Server.Primary,
                        ["latency_ms"] = best.LatencyMs
                    }
                });

                return $"🔧 DNS 分析:\n\n" +
                       $"当前 DNS: {currentDns}\n" +
                       $"推荐 DNS: {best.Server.Name} ({best.Server.Primary})\n" +
                       $"延迟: {best.LatencyMs:F0}ms\n\n" +
                       $"💡 建议: 前往「网络优化」页面一键切换 DNS";
            }
            return "DNS 测试失败，请稍后重试。";
        }

        // IP 检测
        if (lower.Contains("ip") || lower.Contains("公网") || lower.Contains("位置"))
        {
            var info = await _ipDetector.GetIPInfoAsync();
            var ipv6 = await _ipDetector.GetIPv6Async();

            return $"🌐 IP 信息:\n\n" +
                   $"公网 IP: {info.IP}\n" +
                   $"位置: {info.Location}\n" +
                   $"ISP: {info.ISP}\n" +
                   $"IPv6: {ipv6 ?? "不支持"}\n" +
                   $"风险评分: {info.RiskScore}/100";
        }

        // 诊断
        if (lower.Contains("诊断") || lower.Contains("问题") || lower.Contains("卡") || lower.Contains("慢"))
        {
            var health = await _optimizer.EvaluateHealthAsync();

            // 记录诊断事件
            _ = _zepAI.RecordNetworkEventAsync(new NetworkEventData
            {
                EventType = "network_diagnosis",
                AdditionalData = new Dictionary<string, object>
                {
                    ["health_score"] = health.OverallScore,
                    ["grade"] = health.Grade,
                    ["dns_latency"] = health.DnsLatency,
                    ["packet_loss"] = health.PacketLoss
                }
            });

            var diagnosis = $"🔍 网络诊断结果:\n\n";
            diagnosis += $"健康评分: {health.OverallScore}/100 ({health.Grade})\n\n";
            diagnosis += $"{(health.GatewayOk ? "✅" : "❌")} 网关: {(health.GatewayOk ? "正常" : "异常")}\n";
            diagnosis += $"{(health.InternetOk ? "✅" : "❌")} 外网: {(health.InternetOk ? "正常" : "异常")}\n";
            diagnosis += $"{(health.DnsLatency < 50 ? "✅" : "⚠️")} DNS: {health.DnsLatency:F0}ms\n";
            diagnosis += $"{(health.PacketLoss < 5 ? "✅" : "❌")} 丢包: {health.PacketLoss:F0}%\n\n";

            if (health.OverallScore < 70)
            {
                diagnosis += "💡 建议:\n";
                if (!health.GatewayOk) diagnosis += "• 检查路由器连接\n";
                if (health.DnsLatency > 50) diagnosis += "• 尝试更换 DNS\n";
                if (health.PacketLoss > 5) diagnosis += "• 网络不稳定，检查网线或WiFi信号\n";
            }
            else
            {
                diagnosis += "✨ 网络状态良好！";
            }

            return diagnosis;
        }

        // 游戏延迟测试
        if (lower.Contains("游戏") || lower.Contains("延迟") || lower.Contains("ping"))
        {
            var servers = new Dictionary<string, string>
            {
                ["Steam"] = "steam.com",
                ["Xbox Live"] = "xbox.com",
                ["PlayStation"] = "playstation.com",
                ["LOL 韩服"] = "kr.leagueoflegends.com",
                ["LOL 美服"] = "na.leagueoflegends.com"
            };

            var results = "🎮 游戏服务器延迟测试:\n\n";
            foreach (var (name, host) in servers)
            {
                var ping = await _tester.PingAsync(host);
                var status = ping > 0 
                    ? $"{ping:F0}ms {(ping < 50 ? "✅" : ping < 100 ? "⚠️" : "❌")}"
                    : "超时 ❌";
                results += $"{name}: {status}\n";
            }

            return results;
        }

        // 历史/上下文查询
        if (lower.Contains("历史") || lower.Contains("之前") || lower.Contains("上次"))
        {
            var context = await _zepAI.GetUserContextAsync();
            if (!string.IsNullOrEmpty(context))
            {
                return $"📜 根据历史记录:\n\n{context.Substring(0, Math.Min(500, context.Length))}...";
            }
            return "暂无历史记录。";
        }

        // 默认回复
        return "我理解你的问题，但目前我支持以下功能：\n\n" +
               "• 说「测网速」测试带宽\n" +
               "• 说「扫局域网」查看设备\n" +
               "• 说「优化DNS」获取建议\n" +
               "• 说「查IP」查看公网信息\n" +
               "• 说「诊断问题」全面检测\n" +
               "• 说「游戏延迟」测试游戏服务器\n\n" +
               "试试看吧！";
    }

    private string GetSpeedAdvice(double downloadMbps, double pingMs)
    {
        var advice = "💡 分析:\n";

        if (downloadMbps >= 100)
            advice += "• 下载速度优秀，支持 4K 流媒体\n";
        else if (downloadMbps >= 25)
            advice += "• 下载速度良好，支持高清流媒体\n";
        else if (downloadMbps >= 5)
            advice += "• 下载速度一般，建议检查网络\n";
        else
            advice += "• 下载速度较慢，可能影响使用\n";

        if (pingMs < 50)
            advice += "• 延迟极低，适合游戏";
        else if (pingMs < 100)
            advice += "• 延迟正常";
        else
            advice += "• 延迟较高，可能影响游戏体验";

        return advice;
    }
}
