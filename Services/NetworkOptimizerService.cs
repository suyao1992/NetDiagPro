using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NetDiagPro.Services;

/// <summary>
/// 网络优化服务 - DNS优化、网络重置、性能调优
/// </summary>
public class NetworkOptimizerService
{
    // 常用 DNS 服务器
    public static readonly List<DNSServer> DNSServers = new()
    {
        new("Cloudflare", "1.1.1.1", "1.0.0.1"),
        new("Google", "8.8.8.8", "8.8.4.4"),
        new("阿里云", "223.5.5.5", "223.6.6.6"),
        new("腾讯", "119.29.29.29", "119.28.28.28"),
        new("114 DNS", "114.114.114.114", "114.114.115.115"),
        new("OpenDNS", "208.67.222.222", "208.67.220.220"),
        new("Quad9", "9.9.9.9", "149.112.112.112")
    };

    /// <summary>
    /// 测试所有 DNS 服务器延迟
    /// </summary>
    public async Task<List<DNSTestResult>> TestAllDNSAsync(
        Action<string>? statusCallback = null)
    {
        var results = new List<DNSTestResult>();

        foreach (var dns in DNSServers)
        {
            statusCallback?.Invoke($"测试 {dns.Name}...");

            var latency = await TestDNSLatencyAsync(dns.Primary);
            results.Add(new DNSTestResult
            {
                Server = dns,
                LatencyMs = latency,
                Available = latency >= 0
            });
        }

        // 按延迟排序
        return results.OrderBy(r => r.Available ? r.LatencyMs : 9999).ToList();
    }

    /// <summary>
    /// 测试单个 DNS 延迟
    /// </summary>
    public async Task<double> TestDNSLatencyAsync(string dnsServer)
    {
        try
        {
            using var ping = new Ping();
            var times = new List<long>();

            for (int i = 0; i < 3; i++)
            {
                var reply = await ping.SendPingAsync(dnsServer, 2000);
                if (reply.Status == IPStatus.Success)
                {
                    times.Add(reply.RoundtripTime);
                }
            }

            return times.Count > 0 ? times.Average() : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// 获取当前 DNS 设置
    /// </summary>
    public async Task<string> GetCurrentDNSAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface ip show dns",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return "未知";

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 解析 DNS 地址
            var match = Regex.Match(output, @"静态配置的\s+DNS\s+服务器[:：]\s*(\S+)|DNS Servers.*:\s*(\S+)", 
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            }

            return "自动获取";
        }
        catch
        {
            return "未知";
        }
    }

    /// <summary>
    /// 清除 DNS 缓存
    /// </summary>
    public async Task<CommandResult> FlushDNSAsync()
    {
        return await RunCommandAsync("ipconfig", "/flushdns");
    }

    /// <summary>
    /// 释放 IP 地址
    /// </summary>
    public async Task<CommandResult> ReleaseIPAsync()
    {
        return await RunCommandAsync("ipconfig", "/release");
    }

    /// <summary>
    /// 续订 IP 地址
    /// </summary>
    public async Task<CommandResult> RenewIPAsync()
    {
        return await RunCommandAsync("ipconfig", "/renew");
    }

    /// <summary>
    /// 重置 Winsock
    /// </summary>
    public async Task<CommandResult> ResetWinsockAsync()
    {
        return await RunCommandAsync("netsh", "winsock reset", requireAdmin: true);
    }

    /// <summary>
    /// 重置 TCP/IP 栈
    /// </summary>
    public async Task<CommandResult> ResetTCPIPAsync()
    {
        return await RunCommandAsync("netsh", "int ip reset", requireAdmin: true);
    }

    /// <summary>
    /// 检测最佳 MTU 值
    /// </summary>
    public async Task<int> DetectOptimalMTUAsync(string host = "www.google.com")
    {
        int mtu = 1500;

        try
        {
            // 二分查找最佳 MTU
            int low = 1200, high = 1500;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                var psi = new ProcessStartInfo
                {
                    FileName = "ping",
                    Arguments = $"-f -l {mid - 28} {host} -n 1", // -28 for IP+ICMP header
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) break;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (output.Contains("需要分段") || output.Contains("fragmented"))
                {
                    high = mid - 1;
                }
                else if (output.Contains("TTL") || output.Contains("Reply"))
                {
                    mtu = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
        }
        catch { }

        return mtu;
    }

    /// <summary>
    /// 计算网络健康评分
    /// </summary>
    public async Task<NetworkHealthReport> EvaluateHealthAsync(
        Action<string>? statusCallback = null)
    {
        var report = new NetworkHealthReport();

        try
        {
            // 1. DNS 响应测试
            statusCallback?.Invoke("测试 DNS 响应...");
            report.DnsLatency = await TestDNSLatencyAsync("8.8.8.8");
            report.DnsScore = report.DnsLatency switch
            {
                < 0 => 0,
                < 30 => 100,
                < 50 => 80,
                < 100 => 60,
                < 200 => 40,
                _ => 20
            };

            // 2. 网关连通性
            statusCallback?.Invoke("测试网关连通...");
            var gateway = GetDefaultGateway();
            if (!string.IsNullOrEmpty(gateway))
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(gateway, 2000);
                report.GatewayOk = reply.Status == IPStatus.Success;
                report.GatewayLatency = report.GatewayOk ? reply.RoundtripTime : -1;
            }

            // 3. 外网连通性
            statusCallback?.Invoke("测试外网连通...");
            using var pingExt = new Ping();
            var extReply = await pingExt.SendPingAsync("8.8.8.8", 3000);
            report.InternetOk = extReply.Status == IPStatus.Success;
            report.InternetLatency = report.InternetOk ? extReply.RoundtripTime : -1;

            // 4. 丢包率测试
            statusCallback?.Invoke("测试丢包率...");
            int success = 0;
            for (int i = 0; i < 10; i++)
            {
                using var p = new Ping();
                var r = await p.SendPingAsync("8.8.8.8", 1000);
                if (r.Status == IPStatus.Success) success++;
            }
            report.PacketLoss = (10 - success) * 10.0;

            // 计算总分
            report.OverallScore = (int)(
                report.DnsScore * 0.3 +
                (report.GatewayOk ? 100 : 0) * 0.2 +
                (report.InternetOk ? 100 : 0) * 0.3 +
                (100 - report.PacketLoss) * 0.2
            );

            report.Grade = report.OverallScore switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };
        }
        catch (Exception ex)
        {
            report.Error = ex.Message;
        }

        return report;
    }

    /// <summary>
    /// 获取默认网关
    /// </summary>
    private static string? GetDefaultGateway()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;

                var props = nic.GetIPProperties();
                foreach (var gateway in props.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return gateway.Address.ToString();
                    }
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    private async Task<CommandResult> RunCommandAsync(string fileName, string arguments, bool requireAdmin = false)
    {
        var result = new CommandResult();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = !requireAdmin,
                RedirectStandardError = !requireAdmin,
                UseShellExecute = requireAdmin,
                CreateNoWindow = !requireAdmin
            };

            if (requireAdmin)
            {
                psi.Verb = "runas"; // 请求管理员权限
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                result.Success = false;
                result.Error = "无法启动进程";
                return result;
            }

            if (!requireAdmin)
            {
                result.Output = await process.StandardOutput.ReadToEndAsync();
                result.Error = await process.StandardError.ReadToEndAsync();
            }

            await process.WaitForExitAsync();
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }
}

/// <summary>
/// DNS 服务器信息
/// </summary>
public record DNSServer(string Name, string Primary, string Secondary);

/// <summary>
/// DNS 测试结果
/// </summary>
public class DNSTestResult
{
    public DNSServer Server { get; set; } = null!;
    public double LatencyMs { get; set; }
    public bool Available { get; set; }
}

/// <summary>
/// 命令执行结果
/// </summary>
public class CommandResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 网络健康报告
/// </summary>
public class NetworkHealthReport
{
    public int OverallScore { get; set; }
    public string Grade { get; set; } = "?";
    public double DnsLatency { get; set; }
    public int DnsScore { get; set; }
    public bool GatewayOk { get; set; }
    public double GatewayLatency { get; set; }
    public bool InternetOk { get; set; }
    public double InternetLatency { get; set; }
    public double PacketLoss { get; set; }
    public string? Error { get; set; }
}
