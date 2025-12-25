using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class HistoryPage : Page
{
    private readonly DatabaseService _db = new();

    public HistoryPage()
    {
        this.InitializeComponent();
        LoadHistory();
    }

    private async void LoadHistory()
    {
        try
        {
            var records = await _db.GetRecentRecordsAsync(50);
            HistoryList.Items.Clear();
            CountText.Text = $"共 {records.Count} 条记录";

            foreach (var record in records)
            {
                AddHistoryItem(record);
            }
        }
        catch (Exception ex)
        {
            CountText.Text = $"加载失败: {ex.Message}";
        }
    }

    private void AddHistoryItem(TestRecord record)
    {
        var grid = new Grid { Padding = new Thickness(12, 8, 12, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        // Timestamp
        var timeText = new TextBlock 
        { 
            Text = record.Timestamp.ToString("MM-dd HH:mm"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(timeText, 0);

        // Target
        var targetText = new TextBlock { Text = record.Target };
        Grid.SetColumn(targetText, 1);

        // Latency
        var latencyText = new TextBlock 
        { 
            Text = $"{record.LatencyMs:F0} ms",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                record.LatencyMs < 100 ? Microsoft.UI.Colors.Green
                    : record.LatencyMs < 300 ? Microsoft.UI.Colors.Orange
                    : Microsoft.UI.Colors.Red)
        };
        Grid.SetColumn(latencyText, 2);

        // Status
        var statusText = new TextBlock 
        { 
            Text = record.Success ? "成功" : "失败",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                record.Success ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red)
        };
        Grid.SetColumn(statusText, 3);

        grid.Children.Add(timeText);
        grid.Children.Add(targetText);
        grid.Children.Add(latencyText);
        grid.Children.Add(statusText);

        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(4),
            Child = grid
        };

        HistoryList.Items.Add(border);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadHistory();
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "确认清除",
            Content = "确定要清除所有历史记录吗？此操作无法撤销。",
            PrimaryButtonText = "清除",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _db.ClearAllRecordsAsync();
            HistoryList.Items.Clear();
            CountText.Text = "已清除";
        }
    }
}

public class TestRecord
{
    public DateTime Timestamp { get; set; }
    public string Target { get; set; } = "";
    public double LatencyMs { get; set; }
    public bool Success { get; set; }
}
