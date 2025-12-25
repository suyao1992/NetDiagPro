using FlaUI.Core.AutomationElements;

namespace DesktopUiTests.Pages;

/// <summary>
/// Page Object for SpeedTest page
/// </summary>
public class SpeedTestPage : BasePage
{
    // AutomationIds
    public const string StartTestButton = "StartSpeedTestBtn";
    public const string PingResult = "PingResultText";
    public const string DownloadResult = "DownloadResultText";
    public const string UploadResult = "UploadResultText";
    public const string ProgressBar = "SpeedTestProgress";
    public const string StatusText = "SpeedTestStatus";

    public SpeedTestPage(Window mainWindow) : base(mainWindow) { }

    /// <summary>
    /// Click Start Speed Test button
    /// </summary>
    public bool StartTest()
    {
        // Try by AutomationId first, fall back to name
        if (ClickById(StartTestButton, 3))
            return true;
        
        return ClickByName("开始测速", 3) || ClickByName("Start Test", 3);
    }

    /// <summary>
    /// Check if test is running (progress visible)
    /// </summary>
    public bool IsTestRunning()
    {
        var progress = FindById(ProgressBar, 1);
        return progress != null && progress.IsAvailable;
    }

    /// <summary>
    /// Wait for test to complete
    /// </summary>
    public bool WaitForTestComplete(int maxSeconds = 60)
    {
        var start = DateTime.Now;
        
        while ((DateTime.Now - start).TotalSeconds < maxSeconds)
        {
            // Check if any result is showing
            var pingResult = FindById(PingResult, 1);
            if (pingResult != null && !string.IsNullOrEmpty(pingResult.Name) && pingResult.Name != "--")
            {
                return true;
            }
            
            Thread.Sleep(1000);
        }
        
        return false;
    }

    /// <summary>
    /// Get ping result text
    /// </summary>
    public string GetPingResult()
    {
        var element = FindById(PingResult, 3);
        return element?.Name ?? "--";
    }

    /// <summary>
    /// Get download speed result
    /// </summary>
    public string GetDownloadResult()
    {
        var element = FindById(DownloadResult, 3);
        return element?.Name ?? "--";
    }

    /// <summary>
    /// Check if page is loaded by finding start button
    /// </summary>
    public bool IsLoaded()
    {
        var startBtn = FindById(StartTestButton, 3) ?? FindByName("开始测速", 3);
        return startBtn != null && startBtn.IsAvailable;
    }
}
