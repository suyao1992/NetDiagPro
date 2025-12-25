using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using NetDiagPro.Services;
using NetDiagPro.Models;

namespace NetDiagPro.Views;

public sealed partial class LiveMonitorPage : Page
{
    private readonly NetworkTesterService _tester = new();
    private readonly ObservableCollection<MonitorTarget> _targets = new();
    private bool _isRunning = false;
    private CancellationTokenSource? _cts;

    private readonly string[] _defaultTargets = new[]
    {
        "https://www.google.com",
        "https://www.youtube.com",
        "https://twitter.com",
        "https://github.com"
    };

    public LiveMonitorPage()
    {
        this.InitializeComponent();
        InitializeTargets();
    }

    private void InitializeTargets()
    {
        MonitorCards.Items.Clear();
        foreach (var url in _defaultTargets)
        {
            var card = CreateMonitorCard(url);
            MonitorCards.Items.Add(card);
        }
    }

    private Border CreateMonitorCard(string url)
    {
        var domain = new Uri(url).Host;
        
        var card = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Tag = url
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Status indicator
        var statusIcon = new FontIcon
        {
            Glyph = "\uEA3B",
            FontSize = 20,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Tag = "status"
        };
        Grid.SetColumn(statusIcon, 0);

        // Domain info
        var infoStack = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        infoStack.Children.Add(new TextBlock 
        { 
            Text = domain, 
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        var detailsText = new TextBlock 
        { 
            Text = "等待测试...",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Tag = "details"
        };
        infoStack.Children.Add(detailsText);
        Grid.SetColumn(infoStack, 1);

        // Latency display
        var latencyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var latencyText = new TextBlock
        {
            Text = "--",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            Tag = "latency"
        };
        latencyStack.Children.Add(latencyText);
        latencyStack.Children.Add(new TextBlock
        {
            Text = "ms",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(latencyStack, 2);

        grid.Children.Add(statusIcon);
        grid.Children.Add(infoStack);
        grid.Children.Add(latencyStack);
        card.Child = grid;

        return card;
    }

    private async void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _isRunning = false;
            _cts?.Cancel();
            StartStopText.Text = "开始监控";
            StartStopIcon.Glyph = "\uE768";
            LoadingRing.IsActive = false;
        }
        else
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            StartStopText.Text = "停止监控";
            StartStopIcon.Glyph = "\uE71A";
            LoadingRing.IsActive = true;
            
            await RunMonitorLoopAsync(_cts.Token);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        await TestAllTargetsAsync();
        LoadingRing.IsActive = false;
        LastUpdateText.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
    }

    private async Task RunMonitorLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await TestAllTargetsAsync();
            LastUpdateText.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
            
            try
            {
                await Task.Delay(3000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task TestAllTargetsAsync()
    {
        foreach (var item in MonitorCards.Items)
        {
            if (item is Border card && card.Tag is string url)
            {
                try
                {
                    var result = await _tester.TestTargetAsync(url);
                    UpdateCardUI(card, result);
                }
                catch
                {
                    // Handle error
                }
            }
        }
    }

    private void UpdateCardUI(Border card, NetworkTestResult result)
    {
        if (card.Child is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is FontIcon icon && icon.Tag?.ToString() == "status")
                {
                    icon.Glyph = result.Success ? "\uEA3B" : "\uEA39";
                    icon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        result.Success 
                            ? (result.TotalMs < 200 ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Orange)
                            : Microsoft.UI.Colors.Red);
                }
                else if (child is StackPanel stack)
                {
                    foreach (var stackChild in stack.Children)
                    {
                        if (stackChild is TextBlock tb)
                        {
                            if (tb.Tag?.ToString() == "details")
                            {
                                tb.Text = $"TCP: {result.TcpMs:F0}ms | DNS: {result.DnsMs:F0}ms";
                            }
                            else if (tb.Tag?.ToString() == "latency")
                            {
                                tb.Text = result.Success ? $"{result.TotalMs:F0}" : "--";
                            }
                        }
                    }
                }
            }
        }
    }
}
