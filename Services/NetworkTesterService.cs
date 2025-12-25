using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace NetDiagPro.Services;

/// <summary>
/// 网络测试服务 - DNS/TCP/HTTP 详细测试
/// </summary>
public class NetworkTesterService
{
    private readonly HttpClient _httpClient;

    public NetworkTesterService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NetDiagPro/4.0");
    }

    /// <summary>
    /// 测试目标 URL，返回详细时间分解
    /// </summary>
    public async Task<NetworkTestResult> TestTargetAsync(string url)
    {
        var result = new NetworkTestResult
        {
            Target = url,
            Timestamp = DateTime.Now
        };

        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);

            // 1. DNS 解析时间
            var dnsStart = Stopwatch.StartNew();
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host);
                dnsStart.Stop();
                result.DnsMs = dnsStart.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"DNS 解析失败: {ex.Message}";
                return result;
            }

            if (addresses.Length == 0)
            {
                result.Success = false;
                result.Error = "DNS 解析无结果";
                return result;
            }

            // 2. TCP 连接时间
            var tcpStart = Stopwatch.StartNew();
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveTimeout = 5000;
                socket.SendTimeout = 5000;
                await socket.ConnectAsync(addresses[0], port);
                tcpStart.Stop();
                result.TcpMs = tcpStart.Elapsed.TotalMilliseconds;
                socket.Close();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"TCP 连接失败: {ex.Message}";
                return result;
            }

            // 3. HTTP 请求时间
            var httpStart = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync(url);
                httpStart.Stop();
                result.HttpMs = httpStart.Elapsed.TotalMilliseconds;
                result.StatusCode = (int)response.StatusCode;
                result.Success = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"HTTP 请求失败: {ex.Message}";
                return result;
            }

            result.TotalMs = result.DnsMs + result.TcpMs + result.HttpMs;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Ping 测试
    /// </summary>
    public async Task<double> PingAsync(string host, int timeoutMs = 3000)
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
            {
                return reply.RoundtripTime;
            }
        }
        catch { }
        return -1;
    }

    /// <summary>
    /// 批量测试多个目标
    /// </summary>
    public async Task<List<NetworkTestResult>> TestMultipleAsync(
        IEnumerable<string> urls,
        Action<NetworkTestResult>? onResult = null)
    {
        var results = new List<NetworkTestResult>();

        foreach (var url in urls)
        {
            var result = await TestTargetAsync(url);
            results.Add(result);
            onResult?.Invoke(result);
        }

        return results;
    }

    /// <summary>
    /// 计算测试统计信息
    /// </summary>
    public static NetworkStats CalculateStats(IEnumerable<NetworkTestResult> results)
    {
        var successResults = results.Where(r => r.Success).ToList();

        if (successResults.Count == 0)
        {
            return new NetworkStats { PacketLoss = 100 };
        }

        var totalResults = results.Count();
        var latencies = successResults.Select(r => r.TotalMs).ToList();

        return new NetworkStats
        {
            AvgLatency = latencies.Average(),
            MinLatency = latencies.Min(),
            MaxLatency = latencies.Max(),
            Jitter = latencies.Count > 1
                ? latencies.Zip(latencies.Skip(1), (a, b) => Math.Abs(a - b)).Average()
                : 0,
            PacketLoss = (1.0 - (double)successResults.Count / totalResults) * 100,
            SuccessCount = successResults.Count,
            TotalCount = totalResults
        };
    }
}

/// <summary>
/// 网络测试结果
/// </summary>
public class NetworkTestResult
{
    public string Target { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double DnsMs { get; set; }
    public double TcpMs { get; set; }
    public double HttpMs { get; set; }
    public double TotalMs { get; set; }
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 网络统计信息
/// </summary>
public class NetworkStats
{
    public double AvgLatency { get; set; }
    public double MinLatency { get; set; }
    public double MaxLatency { get; set; }
    public double Jitter { get; set; }
    public double PacketLoss { get; set; }
    public int SuccessCount { get; set; }
    public int TotalCount { get; set; }
}
