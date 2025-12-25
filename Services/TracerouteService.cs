using System.Diagnostics;
using System.Text.RegularExpressions;
using NetDiagPro.Models;

namespace NetDiagPro.Services;

/// <summary>
/// 路由追踪服务 - 支持地理位置和ISP信息
/// </summary>
public class TracerouteService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, GeoInfo> _geoCache = new();

    public TracerouteService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>
    /// 执行路由追踪
    /// </summary>
    public async IAsyncEnumerable<TraceHop> TraceRouteAsync(
        string target,
        int maxHops = 20,
        int timeoutMs = 2000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "tracert",
            Arguments = $"-d -h {maxHops} -w {timeoutMs} {target}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding("GB2312")
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var reader = process.StandardOutput;
        // 匹配: 跳数  延迟1  延迟2  延迟3  IP
        var hopPattern = new Regex(@"^\s*(\d+)\s+(?:(\d+)\s*ms|[*<])\s+(?:(\d+)\s*ms|[*<])\s+(?:(\d+)\s*ms|[*<])\s+(\S+)?");

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var match = hopPattern.Match(line);
            if (match.Success)
            {
                var hopNumber = int.Parse(match.Groups[1].Value);
                var isTimeout = line.Contains("*") && !match.Groups[5].Success;

                double latency = 0;
                if (!isTimeout)
                {
                    var times = new List<double>();
                    if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var t1)) times.Add(t1);
                    if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var t2)) times.Add(t2);
                    if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out var t3)) times.Add(t3);
                    latency = times.Count > 0 ? times.Average() : 0;
                }

                var ip = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null;
                var geoInfo = await GetGeoInfoAsync(ip);

                yield return new TraceHop
                {
                    HopNumber = hopNumber,
                    IP = ip,
                    LatencyMs = latency,
                    IsTimeout = isTimeout,
                    Country = geoInfo?.Country ?? "",
                    City = geoInfo?.City ?? "",
                    ISP = geoInfo?.ISP ?? "",
                    IsPrivate = IsPrivateIP(ip)
                };
            }
        }

        await process.WaitForExitAsync();
    }

    /// <summary>
    /// 获取IP地理信息
    /// </summary>
    private async Task<GeoInfo?> GetGeoInfoAsync(string? ip)
    {
        if (string.IsNullOrEmpty(ip) || ip == "*") return null;

        // 私有IP
        if (IsPrivateIP(ip))
        {
            return new GeoInfo { Country = "局域网", City = "Local", ISP = "Private" };
        }

        // 缓存检查
        if (_geoCache.TryGetValue(ip, out var cached))
        {
            return cached;
        }

        try
        {
            var response = await _httpClient.GetStringAsync(
                $"http://ip-api.com/json/{ip}?fields=status,country,city,isp,org");

            var countryMatch = Regex.Match(response, @"""country""\s*:\s*""([^""]+)""");
            var cityMatch = Regex.Match(response, @"""city""\s*:\s*""([^""]+)""");
            var ispMatch = Regex.Match(response, @"""isp""\s*:\s*""([^""]+)""");

            var geo = new GeoInfo
            {
                Country = countryMatch.Success ? countryMatch.Groups[1].Value : "",
                City = cityMatch.Success ? cityMatch.Groups[1].Value : "",
                ISP = ispMatch.Success ? ispMatch.Groups[1].Value : ""
            };

            _geoCache[ip] = geo;
            return geo;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 判断是否为私有IP
    /// </summary>
    private static bool IsPrivateIP(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;

        return ip.StartsWith("192.168.") ||
               ip.StartsWith("10.") ||
               ip.StartsWith("172.16.") ||
               ip.StartsWith("172.17.") ||
               ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") ||
               ip.StartsWith("172.2") ||
               ip.StartsWith("172.30.") ||
               ip.StartsWith("172.31.") ||
               ip.StartsWith("127.") ||
               ip.StartsWith("169.254.");
    }
}

/// <summary>
/// 地理信息
/// </summary>
public class GeoInfo
{
    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public string ISP { get; set; } = "";
}

/// <summary>
/// 路由跳信息
/// </summary>
public class TraceHop
{
    public int HopNumber { get; set; }
    public string? IP { get; set; }
    public double LatencyMs { get; set; }
    public bool IsTimeout { get; set; }
    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public string ISP { get; set; } = "";
    public bool IsPrivate { get; set; }

    public string Location => IsPrivate ? "局域网" :
        string.IsNullOrEmpty(Country) ? "-" :
        string.IsNullOrEmpty(City) ? Country : $"{Country} {City}";
}
