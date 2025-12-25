using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class TraceroutePage : Page
{
    private readonly TracerouteService _traceService = new();

    public TraceroutePage()
    {
        this.InitializeComponent();
        TargetInput.Text = "google.com";
    }

    private async void Trace_Click(object sender, RoutedEventArgs e)
    {
        var target = TargetInput.Text?.Trim();
        if (string.IsNullOrEmpty(target)) return;

        TraceBtn.IsEnabled = false;
        TraceProgress.IsActive = true;
        HopsContainer.Items.Clear();
        StatusText.Text = "正在追踪路由...";

        try
        {
            await foreach (var hop in _traceService.TraceRouteAsync(target))
            {
                AddHopRow(hop);
            }
            StatusText.Text = "追踪完成";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"追踪失败: {ex.Message}";
        }
        finally
        {
            TraceBtn.IsEnabled = true;
            TraceProgress.IsActive = false;
        }
    }

    private void AddHopRow(TraceHop hop)
    {
        var grid = new Grid { Padding = new Thickness(8, 4, 8, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Hop number
        var hopNum = new TextBlock 
        { 
            Text = hop.HopNumber.ToString(), 
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        };
        Grid.SetColumn(hopNum, 0);

        // IP
        var ipText = new TextBlock 
        { 
            Text = hop.IsTimeout ? "*" : (hop.IP ?? "-"),
            Foreground = hop.IsTimeout 
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };
        Grid.SetColumn(ipText, 1);

        // Latency
        var latencyText = new TextBlock 
        { 
            Text = hop.IsTimeout ? "超时" : $"{hop.LatencyMs:F0} ms",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                hop.IsTimeout ? Microsoft.UI.Colors.Red 
                    : (hop.LatencyMs < 100 ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Orange))
        };
        Grid.SetColumn(latencyText, 2);

        // Location
        var locationText = new TextBlock 
        { 
            Text = hop.Location ?? "-",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(locationText, 3);

        grid.Children.Add(hopNum);
        grid.Children.Add(ipText);
        grid.Children.Add(latencyText);
        grid.Children.Add(locationText);

        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(4),
            Child = grid
        };

        HopsContainer.Items.Add(border);
    }
}
