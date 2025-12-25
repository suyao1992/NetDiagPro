using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NetDiagPro.Services;

/// <summary>
/// IP 检测服务 - 公网IP、IPv6、地理位置、DNS泄露检测
/// </summary>
public class IPDetectorService
{
    private readonly HttpClient _httpClient;

    // IP 查询 API
    private readonly string[] _ipApis = new[]
    {
        "https://api.ipify.org?format=json",
        "https://ifconfig.me/ip",
        "https://icanhazip.com"
    };

    public IPDetectorService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// 获取本地 IP
    /// </summary>
    public string GetLocalIP()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 53);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch { }
        return "未知";
    }

    /// <summary>
    /// 获取公网 IP
    /// </summary>
    public async Task<string> GetPublicIPAsync()
    {
        foreach (var api in _ipApis)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(api);
                
                // 尝试解析 JSON 格式
                var ipMatch = Regex.Match(response, @"""ip""\s*:\s*""([^""]+)""");
                if (ipMatch.Success)
                {
                    return ipMatch.Groups[1].Value;
                }
                
                // 纯文本格式
                var ip = response.Trim();
                if (IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }
            catch { }
        }
        return "获取失败";
    }

    /// <summary>
    /// 获取 IPv6 地址
    /// </summary>
    public async Task<string?> GetIPv6Async()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://api6.ipify.org");
            var ip = response.Trim();
            if (ip.Contains(":")) // IPv6 格式
            {
                return ip;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 获取 IP 详细信息 (地理位置、ISP等)
    /// </summary>
    public async Task<IPInfo> GetIPInfoAsync(string? ip = null)
    {
        var info = new IPInfo();

        try
        {
            if (string.IsNullOrEmpty(ip))
            {
                ip = await GetPublicIPAsync();
            }
            info.IP = ip;

            var response = await _httpClient.GetStringAsync(
                $"http://ip-api.com/json/{ip}?fields=status,country,countryCode,region,city,isp,org,as,proxy,hosting");

            info.Country = ExtractJsonValue(response, "country");
            info.CountryCode = ExtractJsonValue(response, "countryCode");
            info.Region = ExtractJsonValue(response, "region");
            info.City = ExtractJsonValue(response, "city");
            info.ISP = ExtractJsonValue(response, "isp");
            info.Org = ExtractJsonValue(response, "org");
            info.ASN = ExtractJsonValue(response, "as");
            info.IsProxy = ExtractJsonValue(response, "proxy") == "true";
            info.IsHosting = ExtractJsonValue(response, "hosting") == "true";

            // 计算风险评分 (0-100, 越高越好)
            info.RiskScore = CalculateRiskScore(info);
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }

        return info;
    }

    /// <summary>
    /// DNS 泄露检测
    /// </summary>
    public async Task<DNSLeakResult> DetectDNSLeakAsync()
    {
        var result = new DNSLeakResult();

        try
        {
            // 使用系统 DNS 解析测试域名
            var testDomains = new[]
            {
                "google.com",
                "cloudflare.com",
                "microsoft.com"
            };

            foreach (var domain in testDomains)
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(domain);
                    // DNS 解析成功说明 DNS 可用
                }
                catch
                {
                    result.HasIssue = true;
                }
            }

            // 获取当前 DNS 服务器
            var psi = new ProcessStartInfo
            {
                FileName = "nslookup",
                Arguments = "localhost",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var serverMatch = Regex.Match(output, @"Server:\s+(.+)");
                if (serverMatch.Success)
                {
                    result.DNSServer = serverMatch.Groups[1].Value.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 获取主机名
    /// </summary>
    public string GetHostname()
    {
        try
        {
            return Dns.GetHostName();
        }
        catch
        {
            return "未知";
        }
    }

    /// <summary>
    /// 计算 IP 风险评分 (0-100, 越高越安全)
    /// </summary>
    private int CalculateRiskScore(IPInfo info)
    {
        int score = 100;

        if (info.IsProxy) score -= 30;
        if (info.IsHosting) score -= 20;
        if (string.IsNullOrEmpty(info.ISP)) score -= 10;

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// 从 JSON 字符串提取值
    /// </summary>
    private string ExtractJsonValue(string json, string key)
    {
        var match = Regex.Match(json, $@"""{key}""\s*:\s*""?([^"",}}]+)""?");
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// 获取风险等级描述
    /// </summary>
    public static (string Level, string Color) GetRiskLevel(int score)
    {
        return score switch
        {
            >= 80 => ("优秀", "#10B981"),
            >= 60 => ("良好", "#3B82F6"),
            >= 40 => ("一般", "#F59E0B"),
            _ => ("风险", "#EF4444")
        };
    }

    /// <summary>
    /// 获取国旗 Emoji
    /// </summary>
    public static string GetCountryFlag(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
            return "🌐";

        var upper = countryCode.ToUpper();
        var flag = string.Concat(
            char.ConvertFromUtf32(0x1F1E6 + upper[0] - 'A'),
            char.ConvertFromUtf32(0x1F1E6 + upper[1] - 'A'));
        return flag;
    }
}

/// <summary>
/// IP 详细信息
/// </summary>
public class IPInfo
{
    public string IP { get; set; } = "";
    public string Country { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string Region { get; set; } = "";
    public string City { get; set; } = "";
    public string ISP { get; set; } = "";
    public string Org { get; set; } = "";
    public string ASN { get; set; } = "";
    public bool IsProxy { get; set; }
    public bool IsHosting { get; set; }
    public int RiskScore { get; set; } = 100;
    public string? Error { get; set; }

    public string Location => string.IsNullOrEmpty(City) ? Country : $"{Country} {City}";
}

/// <summary>
/// DNS 泄露检测结果
/// </summary>
public class DNSLeakResult
{
    public string DNSServer { get; set; } = "";
    public bool HasIssue { get; set; }
    public string? Error { get; set; }
}
