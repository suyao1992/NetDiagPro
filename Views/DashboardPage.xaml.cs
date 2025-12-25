using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Net;
using System.Net.Sockets;

namespace NetDiagPro.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        this.InitializeComponent();
        LoadNetworkInfo();
    }

    private async void LoadNetworkInfo()
    {
        // Get local IP
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    LocalIpText.Text = ip.ToString();
                    break;
                }
            }
        }
        catch
        {
            LocalIpText.Text = "无法获取";
        }

        // Get public IP (async) with geo info
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            // Get public IP and geo info in one request
            var geoResponse = await client.GetStringAsync("http://ip-api.com/json/?fields=query,country,regionName,city,isp,org");
            
            // Parse JSON response
            var ipMatch = System.Text.RegularExpressions.Regex.Match(geoResponse, @"""query""\s*:\s*""([^""]+)""");
            var countryMatch = System.Text.RegularExpressions.Regex.Match(geoResponse, @"""country""\s*:\s*""([^""]+)""");
            var regionMatch = System.Text.RegularExpressions.Regex.Match(geoResponse, @"""regionName""\s*:\s*""([^""]+)""");
            var cityMatch = System.Text.RegularExpressions.Regex.Match(geoResponse, @"""city""\s*:\s*""([^""]+)""");
            var ispMatch = System.Text.RegularExpressions.Regex.Match(geoResponse, @"""isp""\s*:\s*""([^""]+)""");
            
            PublicIpText.Text = ipMatch.Success ? ipMatch.Groups[1].Value : "无法获取";
            
            var location = "";
            if (countryMatch.Success) location = countryMatch.Groups[1].Value;
            if (cityMatch.Success) location += " " + cityMatch.Groups[1].Value;
            IpLocationText.Text = !string.IsNullOrEmpty(location) ? location.Trim() : "--";
            
            IspText.Text = ispMatch.Success ? ispMatch.Groups[1].Value : "--";
        }
        catch
        {
            PublicIpText.Text = "无法获取";
            IpLocationText.Text = "--";
            IspText.Text = "--";
        }
    }

    private void QuickSpeedTest_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to speed test page
        var mainWindow = App.Current.Resources["MainWindow"] as MainWindow;
        // For now, just show a message
    }

    private void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to LAN scan page
    }

    private void Diagnose_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to diagnostics
    }
}
