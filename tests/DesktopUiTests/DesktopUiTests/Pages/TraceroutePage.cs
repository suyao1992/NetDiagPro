using FlaUI.Core.AutomationElements;

namespace DesktopUiTests.Pages;

/// <summary>
/// Page Object for Traceroute page
/// </summary>
public class TraceroutePage : BasePage
{
    // AutomationIds
    public const string HostInput = "TracerouteHostInput";
    public const string StartButton = "StartTracerouteBtn";
    public const string StopButton = "StopTracerouteBtn";
    public const string HopsList = "TracerouteHopsList";
    public const string StatusText = "TracerouteStatus";

    public TraceroutePage(Window mainWindow) : base(mainWindow) { }

    /// <summary>
    /// Enter host/IP to trace
    /// </summary>
    public bool EnterHost(string host)
    {
        var input = FindById(HostInput, 3) ?? FindByName("输入目标地址", 3);
        if (input != null && input.IsAvailable)
        {
            var textBox = input.AsTextBox();
            textBox.Text = host;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Click Start Traceroute button
    /// </summary>
    public bool StartTrace()
    {
        if (ClickById(StartButton, 3))
            return true;
        
        return ClickByName("开始追踪", 3) || ClickByName("Start", 3);
    }

    /// <summary>
    /// Click Stop button
    /// </summary>
    public bool StopTrace()
    {
        if (ClickById(StopButton, 3))
            return true;
        
        return ClickByName("停止", 3) || ClickByName("Stop", 3);
    }

    /// <summary>
    /// Wait for trace to complete or show first hop
    /// </summary>
    public bool WaitForFirstHop(int maxSeconds = 30)
    {
        var start = DateTime.Now;
        
        while ((DateTime.Now - start).TotalSeconds < maxSeconds)
        {
            // Look for any hop result item
            var hopsList = FindById(HopsList, 1);
            if (hopsList != null)
            {
                var hops = hopsList.FindAllChildren();
                if (hops.Length > 0)
                {
                    return true;
                }
            }
            
            Thread.Sleep(500);
        }
        
        return false;
    }

    /// <summary>
    /// Get hop count
    /// </summary>
    public int GetHopCount()
    {
        var hopsList = FindById(HopsList, 3);
        if (hopsList != null)
        {
            return hopsList.FindAllChildren().Length;
        }
        return 0;
    }

    /// <summary>
    /// Check if page is loaded
    /// </summary>
    public bool IsLoaded()
    {
        var startBtn = FindById(StartButton, 3) ?? FindByName("开始追踪", 3);
        return startBtn != null && startBtn.IsAvailable;
    }
}
