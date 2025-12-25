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

        // Get public IP (async)
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var publicIp = await client.GetStringAsync("https://api.ipify.org");
            PublicIpText.Text = publicIp.Trim();
        }
        catch
        {
            PublicIpText.Text = "无法获取";
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
