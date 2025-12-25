using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetDiagPro.Services;

/// <summary>
/// 流量监控服务 - 实时网络流量监控
/// </summary>
public class TrafficMonitorService
{
    private readonly List<ProcessNetworkInfo> _cachedProcesses = new();
    private DateTime _lastUpdate = DateTime.MinValue;

    /// <summary>
    /// 获取所有进程的网络使用信息
    /// </summary>
    public async Task<List<ProcessNetworkInfo>> GetProcessNetworkUsageAsync()
    {
        var results = new List<ProcessNetworkInfo>();

        try
        {
            // 使用 netstat -ano 获取连接信息（不需要管理员权限）
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return results;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 解析 netstat 输出，统计每个 PID 的连接数
            var lines = output.Split('\n');
            var pidConnections = new Dictionary<int, int>();

            foreach (var line in lines)
            {
                if (!line.Contains("ESTABLISHED") && !line.Contains("CLOSE_WAIT") && 
                    !line.Contains("TIME_WAIT") && !line.Contains("LISTENING"))
                    continue;

                var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && int.TryParse(parts[^1], out int pid))
                {
                    if (!pidConnections.ContainsKey(pid))
                        pidConnections[pid] = 0;
                    pidConnections[pid]++;
                }
            }

            // 获取进程名称
            foreach (var (pid, connections) in pidConnections.OrderByDescending(x => x.Value))
            {
                if (pid == 0) continue;
                try
                {
                    var proc = Process.GetProcessById(pid);
                    results.Add(new ProcessNetworkInfo
                    {
                        ProcessName = proc.ProcessName,
                        ConnectionCount = connections
                    });
                }
                catch
                {
                    // 进程可能已退出
                }
            }
        }
        catch { }

        return results.Take(20).ToList();
    }

    /// <summary>
    /// 获取网络接口统计信息
    /// </summary>
    public NetworkInterfaceStats GetInterfaceStats()
    {
        var stats = new NetworkInterfaceStats();

        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    continue;

                var nicStats = nic.GetIPv4Statistics();
                stats.BytesReceived += nicStats.BytesReceived;
                stats.BytesSent += nicStats.BytesSent;
                stats.PacketsReceived += nicStats.UnicastPacketsReceived;
                stats.PacketsSent += nicStats.UnicastPacketsSent;

                if (string.IsNullOrEmpty(stats.InterfaceName))
                {
                    stats.InterfaceName = nic.Name;
                    stats.InterfaceType = nic.NetworkInterfaceType.ToString();
                    stats.Speed = nic.Speed;
                }
            }
        }
        catch { }

        stats.Timestamp = DateTime.Now;
        return stats;
    }

    /// <summary>
    /// 计算实时速度
    /// </summary>
    public (double DownloadSpeed, double UploadSpeed) CalculateSpeed(
        NetworkInterfaceStats previous, 
        NetworkInterfaceStats current)
    {
        var timeDiff = (current.Timestamp - previous.Timestamp).TotalSeconds;
        if (timeDiff <= 0) return (0, 0);

        var downloadBytes = current.BytesReceived - previous.BytesReceived;
        var uploadBytes = current.BytesSent - previous.BytesSent;

        // 转换为 Mbps
        var downloadMbps = (downloadBytes * 8.0 / 1_000_000) / timeDiff;
        var uploadMbps = (uploadBytes * 8.0 / 1_000_000) / timeDiff;

        return (Math.Max(0, downloadMbps), Math.Max(0, uploadMbps));
    }

    /// <summary>
    /// 格式化字节数
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// 进程网络信息
/// </summary>
public class ProcessNetworkInfo
{
    public string ProcessName { get; set; } = "";
    public int ConnectionCount { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
}

/// <summary>
/// 网络接口统计
/// </summary>
public class NetworkInterfaceStats
{
    public string InterfaceName { get; set; } = "";
    public string InterfaceType { get; set; } = "";
    public long Speed { get; set; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public long PacketsReceived { get; set; }
    public long PacketsSent { get; set; }
    public DateTime Timestamp { get; set; }
}
