using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;
using System.Diagnostics;

namespace NetDiagPro.Views;

public sealed partial class ExportDialog : ContentDialog
{
    private readonly ReportExportService _exportService = new();
    private DiagnosticReport? _report;

    public ExportDialog()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// 设置要导出的报告数据
    /// </summary>
    public void SetReportData(DiagnosticReport report)
    {
        _report = report;
    }

    private async void Export_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 阻止对话框关闭
        args.Cancel = true;

        if (_report == null)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Message = "没有可导出的数据，请先运行诊断";
            StatusBar.IsOpen = true;
            return;
        }

        ExportProgress.Visibility = Visibility.Visible;
        StatusBar.IsOpen = false;

        try
        {
            string filePath;
            var selectedRadio = FormatSelector.SelectedItem as RadioButton;
            var format = selectedRadio?.Tag?.ToString() ?? "html";

            switch (format)
            {
                case "csv":
                    filePath = await _exportService.ExportToCsvAsync(_report);
                    break;
                case "json":
                    filePath = await _exportService.ExportToJsonAsync(_report);
                    break;
                default:
                    filePath = await _exportService.ExportToHtmlAsync(_report);
                    break;
            }

            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Message = $"报告已导出到: {filePath}";
            StatusBar.IsOpen = true;

            // 自动打开文件
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            // 允许对话框关闭
            await Task.Delay(1500);
            this.Hide();
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Message = $"导出失败: {ex.Message}";
            StatusBar.IsOpen = true;
        }
        finally
        {
            ExportProgress.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 快速生成当前诊断报告
    /// </summary>
    public static async Task<DiagnosticReport> GenerateReportAsync()
    {
        var report = new DiagnosticReport();

        try
        {
            // IP Info
            var ipService = new IPDetectorService();
            report.LocalIP = ipService.GetLocalIP();
            report.PublicIP = await ipService.GetPublicIPAsync();
            report.IPv6 = await ipService.GetIPv6Async();
            
            var ipInfo = await ipService.GetIPInfoAsync();
            report.Location = ipInfo.Location;
            report.ISP = ipInfo.ISP;

            // Health
            var optimizer = new NetworkOptimizerService();
            var health = await optimizer.EvaluateHealthAsync();
            report.HealthScore = health.OverallScore;
            report.HealthGrade = health.Grade;
            report.DnsLatency = health.DnsLatency;
            report.GatewayOk = health.GatewayOk;
            report.InternetOk = health.InternetOk;
            report.PacketLoss = health.PacketLoss;

            // Speed (quick test)
            var speedService = new SpeedTestService();
            report.PingMs = await speedService.MeasurePingAsync("8.8.8.8");

            // LAN Devices
            var lanService = new LANScannerService();
            var devices = await lanService.ScanNetworkAsync();
            report.LANDevices = devices.Select(d => new LANDeviceInfo
            {
                IP = d.IP,
                MAC = d.MAC,
                Vendor = d.Vendor ?? "",
                DeviceType = d.DeviceType ?? ""
            }).ToList();
        }
        catch { }

        return report;
    }
}
