using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NetDiagPro.Models;

namespace NetDiagPro.Services;

public class LANScannerService
{
    public string LocalIP { get; }
    public string GatewayIP { get; }
    public string LocalMAC { get; }

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public LANScannerService()
    {
        LocalIP = GetLocalIP();
        GatewayIP = GetGatewayIP();
        LocalMAC = GetLocalMAC();
    }

    private string GetLocalIP()
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

    private string GetGatewayIP()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                
                var props = nic.GetIPProperties();
                foreach (var gateway in props.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return gateway.Address.ToString();
                    }
                }
            }
        }
        catch { }
        return "未知";
    }

    private string GetLocalMAC()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var mac = nic.GetPhysicalAddress().ToString();
                    return string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                }
            }
        }
        catch { }
        return "未知";
    }

    public WifiInfo? GetWifiInfo()
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
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (string.IsNullOrEmpty(output) || output.Contains("无接口")) 
            {
                return new WifiInfo { Connected = false };
            }

            var ssidMatch = Regex.Match(output, @"SSID\s*:\s*(.+)", RegexOptions.Multiline);
            var signalMatch = Regex.Match(output, @"信号\s*:\s*(\d+)%|Signal\s*:\s*(\d+)%", RegexOptions.Multiline);
            var speedMatch = Regex.Match(output, @"接收速率.*:\s*(.+)|Receive rate.*:\s*(.+)", RegexOptions.Multiline);

            return new WifiInfo
            {
                Connected = ssidMatch.Success,
                SSID = ssidMatch.Success ? ssidMatch.Groups[1].Value.Trim() : "",
                SignalStrength = signalMatch.Success 
                    ? int.Parse(signalMatch.Groups[1].Success ? signalMatch.Groups[1].Value : signalMatch.Groups[2].Value)
                    : 0,
                LinkSpeed = speedMatch.Success 
                    ? (speedMatch.Groups[1].Success ? speedMatch.Groups[1].Value : speedMatch.Groups[2].Value).Trim()
                    : ""
            };
        }
        catch
        {
            return new WifiInfo { Connected = false };
        }
    }

    public async Task<List<LocalDevice>> ScanNetworkAsync(bool deepScan = false)
    {
        var devices = new List<LocalDevice>();

        // Run arp -a command
        var psi = new ProcessStartInfo
        {
            FileName = "arp",
            Arguments = "-a",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return devices;

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Parse ARP table
        var arpPattern = new Regex(@"(\d+\.\d+\.\d+\.\d+)\s+([\da-fA-F-]+)\s+(\w+)");
        foreach (Match match in arpPattern.Matches(output))
        {
            var ip = match.Groups[1].Value;
            var mac = match.Groups[2].Value.ToUpper().Replace("-", ":");
            var type = match.Groups[3].Value;

            if (type.Contains("static") || mac == "FF:FF:FF:FF:FF:FF") continue;

            var device = new LocalDevice
            {
                IP = ip,
                MAC = mac,
                IsOnline = true
            };

            // Identify special devices
            if (ip == LocalIP)
            {
                device.DeviceType = "self";
                device.DeviceLabel = "本机";
            }
            else if (ip == GatewayIP)
            {
                device.DeviceType = "gateway";
                device.DeviceLabel = "网关";
            }

            // Vendor lookup (simplified)
            device.Vendor = await LookupVendorAsync(mac);
            ClassifyDevice(device);

            devices.Add(device);
        }

        // Ensure self and gateway are in list
        if (!devices.Any(d => d.IP == LocalIP))
        {
            devices.Insert(0, new LocalDevice
            {
                IP = LocalIP,
                MAC = LocalMAC,
                DeviceType = "self",
                DeviceLabel = "本机",
                IsOnline = true
            });
        }

        if (!devices.Any(d => d.IP == GatewayIP))
        {
            devices.Insert(0, new LocalDevice
            {
                IP = GatewayIP,
                MAC = "",
                DeviceType = "gateway",
                DeviceLabel = "网关",
                IsOnline = true
            });
        }

        // Sort: gateway first, then self, then others
        devices = devices.OrderBy(d => d.DeviceType == "gateway" ? 0 : d.DeviceType == "self" ? 1 : 2)
                         .ThenBy(d => d.IP)
                         .ToList();

        return devices;
    }

    private async Task<string> LookupVendorAsync(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length < 8) return "";

        var prefix = mac.Substring(0, 8).Replace(":", "").ToUpper();
        
        try
        {
            var response = await _httpClient.GetStringAsync($"https://api.macvendors.com/{prefix}");
            return response.Trim();
        }
        catch
        {
            return "";
        }
    }

    private void ClassifyDevice(LocalDevice device)
    {
        var vendor = device.Vendor?.ToLower() ?? "";

        // Device classification based on vendor
        var categories = new Dictionary<string, (string category, string label)>
        {
            { "apple", ("apple", "苹果设备") },
            { "xiaomi", ("android", "小米") },
            { "huawei", ("android", "华为") },
            { "oppo", ("android", "OPPO") },
            { "vivo", ("android", "vivo") },
            { "samsung", ("android", "三星") },
            { "tp-link", ("router", "TP-Link") },
            { "netgear", ("router", "Netgear") },
            { "asus", ("router", "华硕") },
            { "intel", ("pc", "电脑") },
            { "dell", ("pc", "戴尔") },
            { "lenovo", ("laptop", "联想") },
            { "vmware", ("vm", "虚拟机") },
        };

        foreach (var (key, value) in categories)
        {
            if (vendor.Contains(key))
            {
                device.DeviceCategory = value.category;
                device.DeviceLabel = value.label;
                return;
            }
        }
    }

    public async Task<bool> PingHostAsync(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 2000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}
