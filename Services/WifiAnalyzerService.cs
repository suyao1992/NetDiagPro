using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NetDiagPro.Services;

/// <summary>
/// WiFi 可用性状态
/// </summary>
public enum WifiAvailability
{
    Available,           // WiFi 可用
    ServiceNotRunning,   // WLAN 服务未运行
    NoAdapter,           // 没有无线网卡
    Disabled,            // WiFi 已禁用
    Unknown              // 未知错误
}

/// <summary>
/// WiFi 状态结果
/// </summary>
public class WifiStatusResult
{
    public WifiAvailability Availability { get; set; }
    public string Message { get; set; } = "";
    public WifiConnectionInfo? Connection { get; set; }
}

/// <summary>
/// Wi-Fi 分析服务 - 信道扫描、干扰检测、优化建议
/// </summary>
public class WifiAnalyzerService
{
    /// <summary>
    /// 检查 WiFi 可用性并获取状态
    /// </summary>
    public async Task<WifiStatusResult> CheckWifiStatusAsync()
    {
        var result = new WifiStatusResult();
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                result.Availability = WifiAvailability.Unknown;
                result.Message = "无法启动 netsh 进程";
                return result;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 检查各种状态 (中英文兼容)
            if (output.Contains("没有运行") || output.Contains("is not running") || 
                error.Contains("没有运行") || error.Contains("is not running"))
            {
                result.Availability = WifiAvailability.ServiceNotRunning;
                result.Message = "无线自动配置服务 (WLAN AutoConfig) 未运行\n请在服务管理器中启动 'WLAN AutoConfig' 服务";
                return result;
            }

            if (output.Contains("无接口") || output.Contains("There is no wireless interface") ||
                output.Contains("没有无线接口"))
            {
                result.Availability = WifiAvailability.NoAdapter;
                result.Message = "未检测到无线网卡\n此设备可能不支持 Wi-Fi 或无线适配器已禁用";
                return result;
            }

            // 检查是否已连接 (支持中英文)
            if (!output.Contains("SSID") && !output.Contains("已连接") && !output.Contains("connected"))
            {
                result.Availability = WifiAvailability.Available;
                result.Message = "Wi-Fi 可用但未连接到任何网络";
                return result;
            }

            // 已连接，解析连接信息
            result.Availability = WifiAvailability.Available;
            result.Connection = ParseWifiConnection(output);
            result.Message = result.Connection != null ? "已连接" : "Wi-Fi 可用";
        }
        catch (Exception ex)
        {
            result.Availability = WifiAvailability.Unknown;
            result.Message = $"检查 WiFi 状态时出错: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 解析 WiFi 连接信息
    /// </summary>
    private WifiConnectionInfo? ParseWifiConnection(string output)
    {
        try
        {
            var ssid = ExtractValue(output, @"^\s*SSID\s*:\s*(.+)$");
            if (string.IsNullOrEmpty(ssid)) return null;

            return new WifiConnectionInfo
            {
                SSID = ssid,
                BSSID = ExtractValue(output, @"BSSID\s*:\s*([0-9a-fA-F:]+)"),
                NetworkType = ExtractValue(output, @"(?:网络类型|Network type)\s*:\s*(.+)"),
                Authentication = ExtractValue(output, @"(?:身份验证|Authentication)\s*:\s*(.+)"),
                Cipher = ExtractValue(output, @"(?:密码|Cipher)\s*:\s*(.+)"),
                Channel = int.TryParse(ExtractValue(output, @"(?:信道|Channel)\s*:\s*(\d+)"), out var ch) ? ch : 0,
                Signal = int.TryParse(ExtractValue(output, @"(?:信号|Signal)\s*:\s*(\d+)%?"), out var sig) ? sig : 0,
                ReceiveRate = ExtractValue(output, @"(?:接收速率|Receive rate)[^:]*:\s*(.+)"),
                TransmitRate = ExtractValue(output, @"(?:传输速率|Transmit rate)[^:]*:\s*(.+)"),
                Band = ExtractValue(output, @"(?:无线电类型|Radio type)\s*:\s*(.+)")
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 扫描周围 Wi-Fi 网络
    /// </summary>
    public async Task<(List<WifiNetwork> Networks, string? Error)> ScanNetworksAsync()
    {
        var networks = new List<WifiNetwork>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show networks mode=bssid",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (networks, "无法启动扫描进程");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 检查错误
            if (output.Contains("没有运行") || error.Contains("没有运行") ||
                output.Contains("is not running") || error.Contains("is not running"))
            {
                return (networks, "WLAN 服务未运行");
            }

            if (output.Contains("无接口") || output.Contains("no wireless interface"))
            {
                return (networks, "未检测到无线网卡");
            }

            // Parse output - 支持中英文
            // 中文: "SSID 1 :" 或 英文: "SSID 1 :"
            var blocks = Regex.Split(output, @"SSID\s+\d+\s*:");
            
            foreach (var block in blocks.Skip(1))
            {
                var network = ParseNetworkBlock(block);
                if (network != null)
                {
                    networks.Add(network);
                }
            }
        }
        catch (Exception ex)
        {
            return (networks, $"扫描出错: {ex.Message}");
        }

        return (networks.OrderByDescending(n => n.Signal).ToList(), null);
    }

    /// <summary>
    /// 获取当前连接的 Wi-Fi 详细信息
    /// </summary>
    public async Task<WifiConnectionInfo?> GetCurrentConnectionAsync()
    {
        var status = await CheckWifiStatusAsync();
        return status.Connection;
    }

    /// <summary>
    /// 分析信道拥挤程度
    /// </summary>
    public async Task<ChannelAnalysis> AnalyzeChannelsAsync()
    {
        var analysis = new ChannelAnalysis();
        var (networks, error) = await ScanNetworksAsync();
        
        // 如果扫描出错，设置错误信息
        if (error != null)
        {
            analysis.Error = error;
            return analysis;
        }

        // 2.4GHz 信道 (1-13)
        var channels24 = new int[14];
        // 5GHz 信道 
        var channels5 = new Dictionary<int, int>();

        foreach (var network in networks)
        {
            if (network.Channel >= 1 && network.Channel <= 13)
            {
                // 2.4GHz - 每个信道影响相邻 2 个信道
                for (int i = Math.Max(1, network.Channel - 2); i <= Math.Min(13, network.Channel + 2); i++)
                {
                    channels24[i]++;
                }
            }
            else if (network.Channel > 30)
            {
                // 5GHz
                if (!channels5.ContainsKey(network.Channel))
                    channels5[network.Channel] = 0;
                channels5[network.Channel]++;
            }
        }

        // 找出最佳 2.4GHz 信道 (1, 6, 11 为非重叠信道)
        var bestChannels = new[] { 1, 6, 11 };
        analysis.Best24Channel = bestChannels.OrderBy(c => channels24[c]).First();
        analysis.Channel24Usage = bestChannels.ToDictionary(c => c, c => channels24[c]);

        // 5GHz 最佳信道
        if (channels5.Any())
        {
            analysis.Best5Channel = channels5.OrderBy(kv => kv.Value).First().Key;
            analysis.Channel5Usage = channels5;
        }

        analysis.TotalNetworks = networks.Count;
        analysis.Networks24GHz = networks.Count(n => n.Channel >= 1 && n.Channel <= 13);
        analysis.Networks5GHz = networks.Count(n => n.Channel > 30);

        // 干扰评估
        var currentConnection = await GetCurrentConnectionAsync();
        if (currentConnection != null && currentConnection.Channel > 0)
        {
            analysis.CurrentChannel = currentConnection.Channel;
            
            if (currentConnection.Channel >= 1 && currentConnection.Channel <= 13)
            {
                var congestion = channels24[currentConnection.Channel];
                analysis.CongestionLevel = congestion switch
                {
                    <= 2 => "低",
                    <= 5 => "中",
                    _ => "高"
                };
                analysis.Recommendation = GetRecommendation(currentConnection.Channel, analysis.Best24Channel, congestion);
            }
        }

        return analysis;
    }

    private static WifiNetwork? ParseNetworkBlock(string block)
    {
        try
        {
            var ssid = ExtractValue(block, @"^\s*(.+?)\s*$", multiline: true)?.Trim();
            if (string.IsNullOrEmpty(ssid)) return null;

            return new WifiNetwork
            {
                SSID = ssid,
                BSSID = ExtractValue(block, @"BSSID \d+\s*:\s*(.+)") ?? "",
                Signal = int.TryParse(ExtractValue(block, @"信号\s*:\s*(\d+)%|Signal\s*:\s*(\d+)%"), out var sig) ? sig : 0,
                Channel = int.TryParse(ExtractValue(block, @"信道\s*:\s*(\d+)|Channel\s*:\s*(\d+)"), out var ch) ? ch : 0,
                NetworkType = ExtractValue(block, @"网络类型\s*:\s*(.+)|Network type\s*:\s*(.+)") ?? "",
                Authentication = ExtractValue(block, @"身份验证\s*:\s*(.+)|Authentication\s*:\s*(.+)") ?? ""
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractValue(string input, string pattern, bool multiline = false)
    {
        var options = multiline ? RegexOptions.Multiline : RegexOptions.None;
        var match = Regex.Match(input, pattern, options);
        if (match.Success)
        {
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success)
                    return match.Groups[i].Value.Trim();
            }
        }
        return "";
    }

    private static string GetRecommendation(int current, int best, int congestion)
    {
        if (current == best && congestion <= 2)
            return "✅ 当前信道是最优选择";
        
        if (current != best && congestion > 3)
            return $"💡 建议切换到信道 {best}，当前信道较拥挤";
        
        if (congestion > 5)
            return $"⚠️ 信道严重拥挤，建议切换到信道 {best} 或使用 5GHz";
        
        return "👍 当前信道状态良好";
    }
}

/// <summary>
/// Wi-Fi 网络信息
/// </summary>
public class WifiNetwork
{
    public string SSID { get; set; } = "";
    public string BSSID { get; set; } = "";
    public int Signal { get; set; }
    public int Channel { get; set; }
    public string NetworkType { get; set; } = "";
    public string Authentication { get; set; } = "";

    public string Band => Channel switch
    {
        >= 1 and <= 13 => "2.4 GHz",
        > 30 => "5 GHz",
        _ => "Unknown"
    };

    public string SignalQuality => Signal switch
    {
        >= 80 => "优秀",
        >= 60 => "良好",
        >= 40 => "一般",
        _ => "较弱"
    };
}

/// <summary>
/// 当前 Wi-Fi 连接信息
/// </summary>
public class WifiConnectionInfo
{
    public string SSID { get; set; } = "";
    public string BSSID { get; set; } = "";
    public string NetworkType { get; set; } = "";
    public string Authentication { get; set; } = "";
    public string Cipher { get; set; } = "";
    public int Channel { get; set; }
    public int Signal { get; set; }
    public string ReceiveRate { get; set; } = "";
    public string TransmitRate { get; set; } = "";
    public string Band { get; set; } = "";
}

/// <summary>
/// 信道分析结果
/// </summary>
public class ChannelAnalysis
{
    public int TotalNetworks { get; set; }
    public int Networks24GHz { get; set; }
    public int Networks5GHz { get; set; }
    public int CurrentChannel { get; set; }
    public int Best24Channel { get; set; }
    public int Best5Channel { get; set; }
    public string CongestionLevel { get; set; } = "未知";
    public string Recommendation { get; set; } = "";
    public string? Error { get; set; }  // 扫描错误信息
    public Dictionary<int, int> Channel24Usage { get; set; } = new();
    public Dictionary<int, int> Channel5Usage { get; set; } = new();
}
