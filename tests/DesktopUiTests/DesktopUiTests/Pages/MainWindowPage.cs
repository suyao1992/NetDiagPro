using FlaUI.Core.AutomationElements;

namespace DesktopUiTests.Pages;

/// <summary>
/// Page Object for Main Window navigation
/// </summary>
public class MainWindowPage : BasePage
{
    // Navigation AutomationIds
    public const string NavDashboard = "NavDashboard";
    public const string NavSpeedTest = "NavSpeedTest";
    public const string NavTraceroute = "NavTraceroute";
    public const string NavLANDiagnostics = "NavLANDiagnostics";
    public const string NavNetworkOptimize = "NavNetworkOptimize";
    public const string NavAIAssistant = "NavAIAssistant";
    public const string NavTrafficMonitor = "NavTrafficMonitor";
    public const string NavWifiAnalysis = "NavWifiAnalysis";
    public const string NavHistory = "NavHistory";
    public const string NavSettings = "NavSettings";

    public MainWindowPage(Window mainWindow) : base(mainWindow) { }

    /// <summary>
    /// Navigate to a page by clicking navigation item
    /// </summary>
    public bool NavigateTo(string navItemName)
    {
        // Try by name first (more reliable for NavigationViewItem)
        return ClickByName(navItemName, 3);
    }

    /// <summary>
    /// Navigate to Dashboard
    /// </summary>
    public bool GoToDashboard() => NavigateTo("概览");

    /// <summary>
    /// Navigate to SpeedTest page
    /// </summary>
    public bool GoToSpeedTest() => NavigateTo("带宽测速");

    /// <summary>
    /// Navigate to Traceroute page
    /// </summary>
    public bool GoToTraceroute() => NavigateTo("路由追踪");

    /// <summary>
    /// Navigate to Network Optimize page
    /// </summary>
    public bool GoToNetworkOptimize() => NavigateTo("网络优化");

    /// <summary>
    /// Navigate to AI Assistant page
    /// </summary>
    public bool GoToAIAssistant() => NavigateTo("AI 助手");

    /// <summary>
    /// Navigate to Traffic Monitor page
    /// </summary>
    public bool GoToTrafficMonitor() => NavigateTo("流量监控");

    /// <summary>
    /// Navigate to Wi-Fi Analysis page
    /// </summary>
    public bool GoToWifiAnalysis() => NavigateTo("Wi-Fi 分析");

    /// <summary>
    /// Navigate to History page
    /// </summary>
    public bool GoToHistory() => NavigateTo("历史记录");

    /// <summary>
    /// Get window title
    /// </summary>
    public string Title => MainWindow.Title;
}
