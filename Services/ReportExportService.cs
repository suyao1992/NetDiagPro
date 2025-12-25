using System.Text;
using System.Text.Json;

namespace NetDiagPro.Services;

/// <summary>
/// 诊断报告导出服务 - 支持 PDF、Excel、JSON 格式
/// </summary>
public class ReportExportService
{
    /// <summary>
    /// 生成 JSON 格式报告
    /// </summary>
    public async Task<string> ExportToJsonAsync(DiagnosticReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        var json = JsonSerializer.Serialize(report, options);
        
        var folder = await GetExportFolderAsync();
        var fileName = $"NetDiagReport_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(folder, fileName);
        
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        
        return filePath;
    }

    /// <summary>
    /// 生成 CSV/Excel 格式报告
    /// </summary>
    public async Task<string> ExportToCsvAsync(DiagnosticReport report)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("NetDiag Pro 诊断报告");
        sb.AppendLine($"生成时间,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"设备名称,{report.MachineName}");
        sb.AppendLine();
        
        // Network Status
        sb.AppendLine("=== 网络状态 ===");
        sb.AppendLine("指标,数值,单位,状态");
        sb.AppendLine($"下载速度,{report.DownloadMbps:F2},Mbps,{GetSpeedStatus(report.DownloadMbps)}");
        sb.AppendLine($"上传速度,{report.UploadMbps:F2},Mbps,{GetSpeedStatus(report.UploadMbps / 2)}");
        sb.AppendLine($"延迟,{report.PingMs:F0},ms,{GetLatencyStatus(report.PingMs)}");
        sb.AppendLine($"丢包率,{report.PacketLoss:F1},%,{(report.PacketLoss < 5 ? "正常" : "异常")}");
        sb.AppendLine();
        
        // Health Score
        sb.AppendLine("=== 健康评分 ===");
        sb.AppendLine($"总分,{report.HealthScore},/100,{report.HealthGrade}");
        sb.AppendLine($"DNS 延迟,{report.DnsLatency:F0},ms,");
        sb.AppendLine($"网关状态,{(report.GatewayOk ? "正常" : "异常")},,");
        sb.AppendLine($"外网状态,{(report.InternetOk ? "正常" : "异常")},,");
        sb.AppendLine();
        
        // IP Info
        sb.AppendLine("=== IP 信息 ===");
        sb.AppendLine($"本地 IP,{report.LocalIP}");
        sb.AppendLine($"公网 IP,{report.PublicIP}");
        sb.AppendLine($"IPv6,{report.IPv6 ?? "不支持"}");
        sb.AppendLine($"位置,{report.Location}");
        sb.AppendLine($"ISP,{report.ISP}");
        sb.AppendLine();
        
        // LAN Devices
        if (report.LANDevices?.Count > 0)
        {
            sb.AppendLine("=== 局域网设备 ===");
            sb.AppendLine("IP,MAC,厂商,类型");
            foreach (var device in report.LANDevices)
            {
                sb.AppendLine($"{device.IP},{device.MAC},{device.Vendor},{device.DeviceType}");
            }
        }
        
        var folder = await GetExportFolderAsync();
        var fileName = $"NetDiagReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(folder, fileName);
        
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        
        return filePath;
    }

    /// <summary>
    /// 生成 HTML 格式报告 (可打印为 PDF)
    /// </summary>
    public async Task<string> ExportToHtmlAsync(DiagnosticReport report)
    {
        var html = $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <title>NetDiag Pro 诊断报告</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; background: #f5f5f5; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 12px; margin-bottom: 20px; }}
        .header h1 {{ margin: 0; font-size: 28px; }}
        .header p {{ margin: 8px 0 0; opacity: 0.9; }}
        .card {{ background: white; border-radius: 12px; padding: 20px; margin-bottom: 16px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .card h2 {{ margin-top: 0; color: #333; font-size: 18px; border-bottom: 2px solid #667eea; padding-bottom: 8px; }}
        .metric {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #eee; }}
        .metric:last-child {{ border-bottom: none; }}
        .metric-label {{ color: #666; }}
        .metric-value {{ font-weight: 600; color: #333; }}
        .status-good {{ color: #10B981; }}
        .status-warn {{ color: #F59E0B; }}
        .status-bad {{ color: #EF4444; }}
        .score-badge {{ display: inline-block; background: #667eea; color: white; padding: 4px 12px; border-radius: 20px; font-weight: 600; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th, td {{ padding: 10px; text-align: left; border-bottom: 1px solid #eee; }}
        th {{ background: #f8f9fa; font-weight: 600; }}
        .footer {{ text-align: center; color: #999; font-size: 12px; margin-top: 20px; }}
        @media print {{ body {{ background: white; }} .card {{ box-shadow: none; border: 1px solid #ddd; }} }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🌐 NetDiag Pro 诊断报告</h1>
        <p>生成时间：{report.GeneratedAt:yyyy-MM-dd HH:mm:ss} | 设备：{report.MachineName}</p>
    </div>

    <div class=""card"">
        <h2>📊 网络性能</h2>
        <div class=""metric"">
            <span class=""metric-label"">下载速度</span>
            <span class=""metric-value"">{report.DownloadMbps:F2} Mbps</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">上传速度</span>
            <span class=""metric-value"">{report.UploadMbps:F2} Mbps</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">网络延迟</span>
            <span class=""metric-value {GetLatencyClass(report.PingMs)}"">{report.PingMs:F0} ms</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">丢包率</span>
            <span class=""metric-value {(report.PacketLoss < 5 ? "status-good" : "status-bad")}"">{report.PacketLoss:F1}%</span>
        </div>
    </div>

    <div class=""card"">
        <h2>🏥 健康评分</h2>
        <div class=""metric"">
            <span class=""metric-label"">综合评分</span>
            <span class=""score-badge"">{report.HealthScore}/100 ({report.HealthGrade})</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">DNS 响应</span>
            <span class=""metric-value"">{report.DnsLatency:F0} ms</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">网关状态</span>
            <span class=""metric-value {(report.GatewayOk ? "status-good" : "status-bad")}"">{(report.GatewayOk ? "✓ 正常" : "✗ 异常")}</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">外网连接</span>
            <span class=""metric-value {(report.InternetOk ? "status-good" : "status-bad")}"">{(report.InternetOk ? "✓ 正常" : "✗ 异常")}</span>
        </div>
    </div>

    <div class=""card"">
        <h2>🌍 IP 信息</h2>
        <div class=""metric"">
            <span class=""metric-label"">本地 IP</span>
            <span class=""metric-value"">{report.LocalIP}</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">公网 IP</span>
            <span class=""metric-value"">{report.PublicIP}</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">IPv6</span>
            <span class=""metric-value"">{report.IPv6 ?? "不支持"}</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">地理位置</span>
            <span class=""metric-value"">{report.Location}</span>
        </div>
        <div class=""metric"">
            <span class=""metric-label"">运营商</span>
            <span class=""metric-value"">{report.ISP}</span>
        </div>
    </div>

    {GetLANDevicesSection(report)}

    <div class=""footer"">
        <p>由 NetDiag Pro 4.0 生成 | 智能网络诊断管理平台</p>
    </div>
</body>
</html>";

        var folder = await GetExportFolderAsync();
        var fileName = $"NetDiagReport_{DateTime.Now:yyyyMMdd_HHmmss}.html";
        var filePath = Path.Combine(folder, fileName);
        
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);
        
        return filePath;
    }

    private static string GetLANDevicesSection(DiagnosticReport report)
    {
        if (report.LANDevices == null || report.LANDevices.Count == 0)
            return "";

        var rows = new StringBuilder();
        foreach (var device in report.LANDevices.Take(20))
        {
            rows.AppendLine($@"<tr>
                <td>{device.IP}</td>
                <td>{device.MAC}</td>
                <td>{device.Vendor}</td>
                <td>{device.DeviceType}</td>
            </tr>");
        }

        return $@"
    <div class=""card"">
        <h2>📡 局域网设备 ({report.LANDevices.Count} 台)</h2>
        <table>
            <tr><th>IP 地址</th><th>MAC 地址</th><th>厂商</th><th>类型</th></tr>
            {rows}
        </table>
    </div>";
    }

    private static string GetSpeedStatus(double mbps)
    {
        return mbps switch
        {
            >= 100 => "优秀",
            >= 25 => "良好",
            >= 5 => "一般",
            _ => "较慢"
        };
    }

    private static string GetLatencyStatus(double ms)
    {
        return ms switch
        {
            < 30 => "优秀",
            < 60 => "良好",
            < 100 => "一般",
            _ => "较高"
        };
    }

    private static string GetLatencyClass(double ms)
    {
        return ms switch
        {
            < 50 => "status-good",
            < 100 => "status-warn",
            _ => "status-bad"
        };
    }

    private static async Task<string> GetExportFolderAsync()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var folder = Path.Combine(documents, "NetDiagPro", "Reports");
        
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        
        return folder;
    }
}

/// <summary>
/// 诊断报告数据模型
/// </summary>
public class DiagnosticReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string MachineName { get; set; } = Environment.MachineName;
    
    // Speed Test
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public double PacketLoss { get; set; }
    
    // Health
    public int HealthScore { get; set; }
    public string HealthGrade { get; set; } = "";
    public double DnsLatency { get; set; }
    public bool GatewayOk { get; set; }
    public bool InternetOk { get; set; }
    
    // IP Info
    public string LocalIP { get; set; } = "";
    public string PublicIP { get; set; } = "";
    public string? IPv6 { get; set; }
    public string Location { get; set; } = "";
    public string ISP { get; set; } = "";
    
    // LAN
    public List<LANDeviceInfo>? LANDevices { get; set; }
}

/// <summary>
/// 局域网设备信息
/// </summary>
public class LANDeviceInfo
{
    public string IP { get; set; } = "";
    public string MAC { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string DeviceType { get; set; } = "";
}
