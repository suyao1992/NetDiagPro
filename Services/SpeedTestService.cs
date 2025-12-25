using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NetDiagPro.Services;

/// <summary>
/// 带宽测速服务 - 支持多服务器、实时回调
/// </summary>
public class SpeedTestService
{
    private readonly HttpClient _httpClient;

    // 测速服务器列表
    private readonly List<SpeedTestServer> _servers = new()
    {
        new("Cloudflare", "https://speed.cloudflare.com/__down?bytes=25000000"),
        new("OVH France", "https://proof.ovh.net/files/10Mb.dat"),
        new("Hetzner Germany", "https://speed.hetzner.de/10MB.bin"),
    };

    public SpeedTestService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NetDiagPro/4.0");
    }

    /// <summary>
    /// 测量 Ping 延迟
    /// </summary>
    public async Task<double> MeasurePingAsync(string host, int attempts = 3)
    {
        try
        {
            using var ping = new Ping();
            var times = new List<long>();

            for (int i = 0; i < attempts; i++)
            {
                var reply = await ping.SendPingAsync(host, 3000);
                if (reply.Status == IPStatus.Success)
                {
                    times.Add(reply.RoundtripTime);
                }
                await Task.Delay(100);
            }

            return times.Count > 0 ? times.Average() : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// 测量下载速度 (Mbps)
    /// </summary>
    public async Task<SpeedTestResult> MeasureDownloadSpeedAsync(
        Action<double>? progressCallback = null,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        var result = new SpeedTestResult();

        foreach (var server in _servers)
        {
            try
            {
                statusCallback?.Invoke($"正在连接 {server.Name}...");

                var stopwatch = Stopwatch.StartNew();
                long bytesDownloaded = 0;

                using var response = await _httpClient.GetAsync(
                    server.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                if (!response.IsSuccessStatusCode) continue;

                result.ServerName = server.Name;
                statusCallback?.Invoke($"正在从 {server.Name} 下载测试...");

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[81920];
                int bytesRead;
                var lastReport = DateTime.Now;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    bytesDownloaded += bytesRead;

                    // 每 200ms 报告一次进度
                    if ((DateTime.Now - lastReport).TotalMilliseconds > 200)
                    {
                        var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                        if (elapsedSec > 0.1)
                        {
                            var speedMbps = (bytesDownloaded * 8.0 / 1_000_000) / elapsedSec;
                            progressCallback?.Invoke(speedMbps);
                        }
                        lastReport = DateTime.Now;
                    }
                }

                stopwatch.Stop();
                var totalSeconds = stopwatch.Elapsed.TotalSeconds;
                if (totalSeconds > 0)
                {
                    result.DownloadMbps = (bytesDownloaded * 8.0 / 1_000_000) / totalSeconds;
                    result.Success = true;
                }

                break; // 成功后停止尝试其他服务器
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                // 继续尝试下一个服务器
            }
        }

        return result;
    }

    /// <summary>
    /// 测量上传速度 (Mbps)
    /// </summary>
    public async Task<SpeedTestResult> MeasureUploadSpeedAsync(
        Action<double>? progressCallback = null,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        var result = new SpeedTestResult();

        try
        {
            statusCallback?.Invoke("正在准备上传测试...");

            // 生成测试数据 (2MB)
            var data = new byte[2_000_000];
            new Random().NextBytes(data);

            var stopwatch = Stopwatch.StartNew();

            using var content = new ByteArrayContent(data);
            var response = await _httpClient.PostAsync(
                "https://httpbin.org/post",
                content,
                ct);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                result.UploadMbps = (data.Length * 8.0 / 1_000_000) / stopwatch.Elapsed.TotalSeconds;
                result.Success = true;
                progressCallback?.Invoke(result.UploadMbps);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 完整测速 (Ping + 下载 + 上传)
    /// </summary>
    public async Task<SpeedTestResult> RunFullTestAsync(
        Action<double>? downloadCallback = null,
        Action<double>? uploadCallback = null,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        var result = new SpeedTestResult();

        // Ping 测试
        statusCallback?.Invoke("正在测试延迟...");
        result.PingMs = await MeasurePingAsync("www.google.com");
        if (result.PingMs < 0)
        {
            result.PingMs = await MeasurePingAsync("www.baidu.com");
        }

        // 下载测试
        statusCallback?.Invoke("正在测试下载速度...");
        var downloadResult = await MeasureDownloadSpeedAsync(downloadCallback, statusCallback, ct);
        result.DownloadMbps = downloadResult.DownloadMbps;
        result.ServerName = downloadResult.ServerName;

        // 上传测试
        statusCallback?.Invoke("正在测试上传速度...");
        var uploadResult = await MeasureUploadSpeedAsync(uploadCallback, statusCallback, ct);
        result.UploadMbps = uploadResult.UploadMbps;

        result.Success = downloadResult.Success || uploadResult.Success;
        statusCallback?.Invoke("测速完成");

        return result;
    }

    /// <summary>
    /// 测试流媒体平台连通性
    /// </summary>
    public async Task<(bool Available, double Latency)> TestPlatformAsync(string url)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            stopwatch.Stop();

            return (response.IsSuccessStatusCode, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch
        {
            return (false, 0);
        }
    }
}

/// <summary>
/// 测速服务器信息
/// </summary>
public record SpeedTestServer(string Name, string Url);

/// <summary>
/// 测速结果
/// </summary>
public class SpeedTestResult
{
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public string ServerName { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}
