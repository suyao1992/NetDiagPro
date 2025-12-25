using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;
using System.Diagnostics;
using FlaUIApp = FlaUI.Core.Application;

namespace DesktopUiTests;

/// <summary>
/// UI Smoke Tests for NetDiagPro using FlaUI (UIA3)
/// </summary>
public class UiSmokeTests : IDisposable
{
    private const string AppPath = @"F:\net-test\NetDiagPro.WinUI\bin\x64\Debug\net8.0-windows10.0.22621.0\NetDiagPro.exe";
    private const string ArtifactsPath = @"F:\net-test\NetDiagPro.WinUI\tests\DesktopUiTests\artifacts";
    private const int MaxWaitSeconds = 10;
    private const int RetryIntervalMs = 200;

    private FlaUIApp? _app;
    private UIA3Automation? _automation;

    public void Dispose()
    {
        // Ensure application is closed after test
        try
        {
            _app?.Close();
            _app?.Dispose();
        }
        catch
        {
            // Force kill if close fails
            try
            {
                _app?.Kill();
            }
            catch { }
        }
        
        _automation?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Smoke Test: Launch application and verify main window appears within 10 seconds
    /// </summary>
    [Fact]
    public void SmokeTest_ApplicationLaunches_MainWindowAppears()
    {
        Window? mainWindow = null;
        
        try
        {
            // Verify app exists
            Assert.True(File.Exists(AppPath), $"Application not found at: {AppPath}");

            // Launch application
            _automation = new UIA3Automation();
            _app = FlaUIApp.Launch(AppPath);
            
            Assert.NotNull(_app);

            // Wait for main window with retry
            mainWindow = WaitForMainWindow(_app, _automation, MaxWaitSeconds);
            
            // Assert main window exists
            Assert.NotNull(mainWindow);
            
            // Capture success screenshot
            CaptureScreenshot(mainWindow, "SmokeTest_Success");
        }
        catch (Exception ex)
        {
            // Capture failure screenshot (desktop if window not available)
            CaptureFailureScreenshot(mainWindow, ex);
            throw;
        }
    }

    /// <summary>
    /// Wait for main window with retry logic
    /// </summary>
    private static Window? WaitForMainWindow(FlaUIApp app, UIA3Automation automation, int maxSeconds)
    {
        var stopwatch = Stopwatch.StartNew();
        Window? mainWindow = null;

        while (stopwatch.Elapsed.TotalSeconds < maxSeconds)
        {
            try
            {
                mainWindow = app.GetMainWindow(automation);
                
                if (mainWindow != null && mainWindow.IsAvailable)
                {
                    return mainWindow;
                }
            }
            catch
            {
                // Window not ready yet, continue retry
            }

            Thread.Sleep(RetryIntervalMs);
        }

        return mainWindow;
    }

    /// <summary>
    /// Capture screenshot of window or desktop on failure
    /// </summary>
    private void CaptureFailureScreenshot(Window? window, Exception ex)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"SmokeTest_FAILED_{timestamp}.png";
        var fullPath = Path.Combine(ArtifactsPath, filename);

        try
        {
            // Ensure artifacts directory exists
            Directory.CreateDirectory(ArtifactsPath);

            if (window != null && window.IsAvailable)
            {
                // Capture window screenshot
                var image = Capture.Element(window);
                image.ToFile(fullPath);
            }
            else
            {
                // Capture full desktop if window not available
                var image = Capture.Screen();
                image.ToFile(fullPath);
            }

            Console.WriteLine($"[SCREENSHOT] Failure captured: {fullPath}");
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        catch (Exception captureEx)
        {
            Console.WriteLine($"[WARNING] Failed to capture screenshot: {captureEx.Message}");
        }
    }

    /// <summary>
    /// Capture success screenshot
    /// </summary>
    private static void CaptureScreenshot(Window window, string prefix)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"{prefix}_{timestamp}.png";
        var fullPath = Path.Combine(ArtifactsPath, filename);

        try
        {
            Directory.CreateDirectory(ArtifactsPath);
            
            if (window.IsAvailable)
            {
                var image = Capture.Element(window);
                image.ToFile(fullPath);
                Console.WriteLine($"[SCREENSHOT] Success captured: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Failed to capture success screenshot: {ex.Message}");
        }
    }
}
