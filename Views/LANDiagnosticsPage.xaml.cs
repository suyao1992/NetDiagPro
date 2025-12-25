using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;
using NetDiagPro.Models;
using System.Net.NetworkInformation;

namespace NetDiagPro.Views;

public sealed partial class LANDiagnosticsPage : Page
{
    private readonly LANScannerService _scanner = new();

    public LANDiagnosticsPage()
    {
        this.InitializeComponent();
        LoadNetworkInfo();
    }

    private void LoadNetworkInfo()
    {
        try
        {
            LocalIpText.Text = $"本机 IP: {_scanner.LocalIP}";
            GatewayText.Text = $"网关: {_scanner.GatewayIP}";
            MacText.Text = $"MAC: {_scanner.LocalMAC}";

            var wifiInfo = _scanner.GetWifiInfo();
            if (wifiInfo != null && wifiInfo.Connected)
            {
                WifiSsidText.Text = $"SSID: {wifiInfo.SSID}";
                WifiSignalText.Text = $"信号: {wifiInfo.SignalStrength}%";
                WifiSpeedText.Text = $"速率: {wifiInfo.LinkSpeed}";
            }
            else
            {
                WifiSsidText.Text = "SSID: 未连接 WiFi";
                WifiSignalText.Text = "信号: -";
                WifiSpeedText.Text = "速率: -";
            }
        }
        catch (Exception ex)
        {
            LocalIpText.Text = $"错误: {ex.Message}";
        }
    }

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        await ScanDevicesAsync(deepScan: false);
    }

    private async void DeepScan_Click(object sender, RoutedEventArgs e)
    {
        await ScanDevicesAsync(deepScan: true);
    }

    private async Task ScanDevicesAsync(bool deepScan)
    {
        QuickScanBtn.IsEnabled = false;
        DeepScanBtn.IsEnabled = false;
        ScanProgress.IsActive = true;
        DeviceList.Items.Clear();

        try
        {
            var devices = await _scanner.ScanNetworkAsync(deepScan);
            DeviceCountText.Text = $"({devices.Count} 台设备)";

            foreach (var device in devices)
            {
                AddDeviceCard(device);
            }
        }
        catch (Exception ex)
        {
            DeviceCountText.Text = $"扫描失败: {ex.Message}";
        }
        finally
        {
            QuickScanBtn.IsEnabled = true;
            DeepScanBtn.IsEnabled = true;
            ScanProgress.IsActive = false;
        }
    }

    private void AddDeviceCard(LocalDevice device)
    {
        var grid = new Grid { Padding = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Icon
        var icon = new FontIcon
        {
            Glyph = device.DeviceType switch
            {
                "gateway" => "\uE839",  // Router
                "self" => "\uE7F4",     // PC
                _ => device.DeviceCategory switch
                {
                    "apple" or "android" => "\uE8EA",  // Phone
                    "pc" or "laptop" => "\uE7F4",      // Computer
                    "router" => "\uE839",              // Router
                    "tv" => "\uE8B2",                  // TV
                    _ => "\uE975"                       // Device
                }
            },
            FontSize = 24,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                device.DeviceType == "gateway" ? Microsoft.UI.Colors.DodgerBlue
                    : device.DeviceType == "self" ? Microsoft.UI.Colors.Green
                    : Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(icon, 0);

        // Info
        var infoStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        var typeLabel = device.DeviceType switch
        {
            "gateway" => "🌐 网关",
            "self" => "💻 本机",
            _ => device.DeviceLabel ?? "设备"
        };
        infoStack.Children.Add(new TextBlock 
        { 
            Text = $"{typeLabel} - {device.IP}",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        infoStack.Children.Add(new TextBlock 
        { 
            Text = $"MAC: {device.MAC}",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        if (!string.IsNullOrEmpty(device.Vendor))
        {
            infoStack.Children.Add(new TextBlock 
            { 
                Text = $"厂商: {device.Vendor}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            });
        }
        if (!string.IsNullOrEmpty(device.Hostname))
        {
            infoStack.Children.Add(new TextBlock 
            { 
                Text = $"主机名: {device.Hostname}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Cyan)
            });
        }
        Grid.SetColumn(infoStack, 1);

        // Status
        var statusIcon = new FontIcon
        {
            Glyph = device.IsOnline ? "\uEA3B" : "\uEA39",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                device.IsOnline ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red),
            FontSize = 16
        };
        Grid.SetColumn(statusIcon, 2);

        grid.Children.Add(icon);
        grid.Children.Add(infoStack);
        grid.Children.Add(statusIcon);

        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(8),
            Child = grid
        };

        DeviceList.Items.Add(border);
    }

    private async void Diagnose_Click(object sender, RoutedEventArgs e)
    {
        DiagnoseBtn.IsEnabled = false;
        ScanProgress.IsActive = true;
        DiagnosticsCard.Visibility = Visibility.Visible;
        DiagnosticsResults.Items.Clear();

        try
        {
            // Gateway test
            var gatewayOk = await _scanner.PingHostAsync(_scanner.GatewayIP);
            AddDiagnosticResult(
                gatewayOk ? "✅" : "❌",
                gatewayOk ? "网关连通" : "网关不通",
                $"{_scanner.GatewayIP}",
                gatewayOk);

            // DNS test
            var dnsOk = await _scanner.PingHostAsync("8.8.8.8");
            AddDiagnosticResult(
                dnsOk ? "✅" : "❌",
                dnsOk ? "DNS 可用" : "DNS 不可用",
                "8.8.8.8",
                dnsOk);

            // Internet test
            var internetOk = await _scanner.PingHostAsync("www.baidu.com");
            AddDiagnosticResult(
                internetOk ? "✅" : "❌",
                internetOk ? "外网连通" : "外网不通",
                "www.baidu.com",
                internetOk);
        }
        catch (Exception ex)
        {
            AddDiagnosticResult("❌", "诊断失败", ex.Message, false);
        }
        finally
        {
            DiagnoseBtn.IsEnabled = true;
            ScanProgress.IsActive = false;
        }
    }

    private void AddDiagnosticResult(string icon, string title, string detail, bool success)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = icon, FontSize = 14 });
        stack.Children.Add(new TextBlock 
        { 
            Text = title, 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                success ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red)
        });
        stack.Children.Add(new TextBlock 
        { 
            Text = detail,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        DiagnosticsResults.Items.Add(stack);
    }
}
