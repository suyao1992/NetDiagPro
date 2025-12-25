namespace NetDiagPro.Services;

/// <summary>
/// 流媒体平台检测服务
/// </summary>
public class StreamingTesterService
{
    private readonly HttpClient _httpClient;

    // 画质带宽要求 (Mbps)
    private readonly Dictionary<string, double> _qualityRequirements = new()
    {
        { "360p", 1.5 },
        { "480p", 3.0 },
        { "720p", 5.0 },
        { "1080p", 8.0 },
        { "1440p (2K)", 16.0 },
        { "2160p (4K)", 25.0 },
        { "4320p (8K)", 50.0 }
    };

    // 流媒体平台列表
    private readonly List<StreamingPlatform> _platforms = new()
    {
        new("YouTube", "🎬", "https://www.youtube.com", new[] { "https://www.youtube.com" }),
        new("Netflix", "🎥", "https://www.netflix.com", new[] { "https://fast.com" }),
        new("Disney+", "🏰", "https://www.disneyplus.com", new[] { "https://www.disneyplus.com" }),
        new("Twitch", "🎮", "https://www.twitch.tv", new[] { "https://www.twitch.tv" }),
        new("Bilibili", "📺", "https://www.bilibili.com", new[] { "https://www.bilibili.com" }),
        new("优酷", "📹", "https://www.youku.com", new[] { "https://www.youku.com" }),
        new("爱奇艺", "🎞️", "https://www.iqiyi.com", new[] { "https://www.iqiyi.com" }),
        new("腾讯视频", "📽️", "https://v.qq.com", new[] { "https://v.qq.com" }),
        new("Amazon Prime", "📦", "https://www.primevideo.com", new[] { "https://www.primevideo.com" }),
        new("HBO Max", "🎭", "https://www.max.com", new[] { "https://www.max.com" })
    };

    public StreamingTesterService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NetDiagPro/4.0");
    }

    /// <summary>
    /// 获取所有平台列表
    /// </summary>
    public IReadOnlyList<StreamingPlatform> Platforms => _platforms.AsReadOnly();

    /// <summary>
    /// 测试单个流媒体平台
    /// </summary>
    public async Task<StreamingTestResult> TestPlatformAsync(StreamingPlatform platform, double speedMbps = 0)
    {
        var result = new StreamingTestResult
        {
            Platform = platform.Name,
            Icon = platform.Icon
        };

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(platform.TestUrl, HttpCompletionOption.ResponseHeadersRead);
            stopwatch.Stop();

            result.Available = response.IsSuccessStatusCode;
            result.LatencyMs = stopwatch.Elapsed.TotalMilliseconds;

            if (result.Available && speedMbps > 0)
            {
                var (recommended, max) = GetRecommendedQuality(speedMbps);
                result.RecommendedQuality = recommended;
                result.MaxQuality = max;
                result.BufferTime4K = EstimateBufferTime(speedMbps, "4K");
            }

            // 尝试获取服务器地区
            result.ServerRegion = await GetServerRegionAsync(platform.TestUrl);
        }
        catch (Exception ex)
        {
            result.Available = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 测试所有流媒体平台
    /// </summary>
    public async Task<List<StreamingTestResult>> TestAllPlatformsAsync(
        double speedMbps = 0,
        Action<int, int, string>? progressCallback = null)
    {
        var results = new List<StreamingTestResult>();
        int current = 0;

        foreach (var platform in _platforms)
        {
            current++;
            progressCallback?.Invoke(current, _platforms.Count, platform.Name);

            var result = await TestPlatformAsync(platform, speedMbps);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 根据网速获取推荐画质
    /// </summary>
    public (string Recommended, string Max) GetRecommendedQuality(double speedMbps)
    {
        string recommended = "360p";
        string max = "360p";

        foreach (var (quality, required) in _qualityRequirements)
        {
            if (speedMbps >= required * 1.5) // 1.5倍余量为推荐
            {
                recommended = quality;
            }
            if (speedMbps >= required) // 刚好满足为最大支持
            {
                max = quality;
            }
        }

        return (recommended, max);
    }

    /// <summary>
    /// 估算缓冲时间 (秒)
    /// </summary>
    public double EstimateBufferTime(double speedMbps, string quality = "4K")
    {
        if (speedMbps <= 0) return 999;

        var requiredMbps = quality switch
        {
            "4K" or "2160p" => 25.0,
            "2K" or "1440p" => 16.0,
            "1080p" => 8.0,
            "720p" => 5.0,
            _ => 3.0
        };

        // 加载10秒视频需要的时间
        var videoSizeMb = requiredMbps * 10 / 8; // MB
        return videoSizeMb / (speedMbps / 8);
    }

    /// <summary>
    /// 获取服务器地区
    /// </summary>
    private async Task<string> GetServerRegionAsync(string url)
    {
        try
        {
            var uri = new Uri(url);
            var addresses = await System.Net.Dns.GetHostAddressesAsync(uri.Host);
            if (addresses.Length > 0)
            {
                var ip = addresses[0].ToString();
                var response = await _httpClient.GetStringAsync($"http://ip-api.com/json/{ip}?fields=country,city");
                
                var countryMatch = System.Text.RegularExpressions.Regex.Match(response, @"""country""\s*:\s*""([^""]+)""");
                if (countryMatch.Success)
                {
                    return countryMatch.Groups[1].Value;
                }
            }
        }
        catch { }

        return "";
    }

    /// <summary>
    /// 获取画质对应的图标
    /// </summary>
    public static string GetQualityIcon(string quality)
    {
        return quality switch
        {
            "4320p (8K)" => "🎯",
            "2160p (4K)" => "🏆",
            "1440p (2K)" => "⭐",
            "1080p" => "✨",
            "720p" => "👍",
            _ => "📺"
        };
    }
}

/// <summary>
/// 流媒体平台信息
/// </summary>
public record StreamingPlatform(
    string Name,
    string Icon,
    string TestUrl,
    string[] CdnUrls);

/// <summary>
/// 流媒体测试结果
/// </summary>
public class StreamingTestResult
{
    public string Platform { get; set; } = "";
    public string Icon { get; set; } = "";
    public bool Available { get; set; }
    public double LatencyMs { get; set; }
    public string ServerRegion { get; set; } = "";
    public string RecommendedQuality { get; set; } = "";
    public string MaxQuality { get; set; } = "";
    public double BufferTime4K { get; set; }
    public string? Error { get; set; }
}
