using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NetDiagPro.Services;

/// <summary>
/// Wi-Fi 分析服务 - 信道扫描、干扰检测、优化建议
/// </summary>
public class WifiAnalyzerService
{
    /// <summary>
    /// 扫描周围 Wi-Fi 网络
    /// </summary>
    public async Task<List<WifiNetwork>> ScanNetworksAsync()
    {
        var networks = new List<WifiNetwork>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show networks mode=bssid",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return networks;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse output
            var blocks = Regex.Split(output, @"SSID \d+ :");
            
            foreach (var block in blocks.Skip(1))
            {
                var network = ParseNetworkBlock(block);
                if (network != null)
                {
                    networks.Add(network);
                }
            }
        }
        catch { }

        return networks.OrderByDescending(n => n.Signal).ToList();
    }

    /// <summary>
    /// 获取当前连接的 Wi-Fi 详细信息
    /// </summary>
    public async Task<WifiConnectionInfo?> GetCurrentConnectionAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (output.Contains("无接口") || !output.Contains("SSID"))
                return null;

            return new WifiConnectionInfo
            {
                SSID = ExtractValue(output, @"SSID\s*:\s*(.+)"),
                BSSID = ExtractValue(output, @"BSSID\s*:\s*(.+)"),
                NetworkType = ExtractValue(output, @"网络类型\s*:\s*(.+)|Network type\s*:\s*(.+)"),
                Authentication = ExtractValue(output, @"身份验证\s*:\s*(.+)|Authentication\s*:\s*(.+)"),
                Cipher = ExtractValue(output, @"密码\s*:\s*(.+)|Cipher\s*:\s*(.+)"),
                Channel = int.TryParse(ExtractValue(output, @"信道\s*:\s*(\d+)|Channel\s*:\s*(\d+)"), out var ch) ? ch : 0,
                Signal = int.TryParse(ExtractValue(output, @"信号\s*:\s*(\d+)%|Signal\s*:\s*(\d+)%"), out var sig) ? sig : 0,
                ReceiveRate = ExtractValue(output, @"接收速率.*:\s*(.+)|Receive rate.*:\s*(.+)"),
                TransmitRate = ExtractValue(output, @"传输速率.*:\s*(.+)|Transmit rate.*:\s*(.+)"),
                Band = ExtractValue(output, @"无线电类型\s*:\s*(.+)|Radio type\s*:\s*(.+)")
            };
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 分析信道拥挤程度
    /// </summary>
    public async Task<ChannelAnalysis> AnalyzeChannelsAsync()
    {
        var analysis = new ChannelAnalysis();
        var networks = await ScanNetworksAsync();

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
    public Dictionary<int, int> Channel24Usage { get; set; } = new();
    public Dictionary<int, int> Channel5Usage { get; set; } = new();
}
