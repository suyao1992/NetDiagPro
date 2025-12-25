namespace NetDiagPro.Models;

public class TestResult
{
    public string Target { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double DnsMs { get; set; }
    public double TcpMs { get; set; }
    public double TotalMs { get; set; }
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class LocalDevice
{
    public string IP { get; set; } = "";
    public string MAC { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string DeviceType { get; set; } = "device";
    public string DeviceCategory { get; set; } = "unknown";
    public string DeviceLabel { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string OpenPorts { get; set; } = "";
    public bool IsOnline { get; set; } = true;
    public double ResponseTimeMs { get; set; }
}

public class WifiInfo
{
    public bool Connected { get; set; }
    public string SSID { get; set; } = "";
    public int SignalStrength { get; set; }
    public string LinkSpeed { get; set; } = "";
}

public class MonitorTarget
{
    public string Url { get; set; } = "";
    public string Domain { get; set; } = "";
    public double TcpMs { get; set; }
    public double HttpMs { get; set; }
    public double DnsMs { get; set; }
    public string Status { get; set; } = "waiting";
}
