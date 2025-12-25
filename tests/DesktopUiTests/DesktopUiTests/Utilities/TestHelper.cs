using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;
using System.Diagnostics;
using FlaUIApp = FlaUI.Core.Application;

namespace DesktopUiTests.Utilities;

/// <summary>
/// Test failure types for categorized screenshots
/// </summary>
public enum FailureType
{
    UI,
    Timeout,
    Crash,
    Assertion,
    Unknown
}

/// <summary>
/// Common test utilities for FlaUI automation
/// </summary>
public static class TestHelper
{
    public const string AppPath = @"F:\net-test\NetDiagPro.WinUI\bin\x64\Debug\net8.0-windows10.0.22621.0\NetDiagPro.exe";
    public const string ArtifactsPath = @"F:\net-test\NetDiagPro.WinUI\tests\DesktopUiTests\artifacts";
    public const int DefaultTimeoutSeconds = 10;
    public const int RetryIntervalMs = 200;

    /// <summary>
    /// Launch application and return app + automation instances
    /// </summary>
    public static (FlaUIApp App, UIA3Automation Automation) LaunchApp()
    {
        if (!File.Exists(AppPath))
        {
            throw new FileNotFoundException($"Application not found: {AppPath}");
        }

        var automation = new UIA3Automation();
        var app = FlaUIApp.Launch(AppPath);
        
        return (app, automation);
    }

    /// <summary>
    /// Wait for main window with retry logic
    /// </summary>
    public static Window? WaitForMainWindow(FlaUIApp app, UIA3Automation automation, int maxSeconds = DefaultTimeoutSeconds)
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
                // Window not ready yet
            }

            Thread.Sleep(RetryIntervalMs);
        }

        return mainWindow;
    }

    /// <summary>
    /// Wait for element by AutomationId with retry
    /// </summary>
    public static AutomationElement? WaitForElement(AutomationElement parent, string automationId, int maxSeconds = DefaultTimeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed.TotalSeconds < maxSeconds)
        {
            try
            {
                var element = parent.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                
                if (element != null && element.IsAvailable)
                {
                    return element;
                }
            }
            catch
            {
                // Element not ready yet
            }

            Thread.Sleep(RetryIntervalMs);
        }

        return null;
    }

    /// <summary>
    /// Wait for element by Name with retry
    /// </summary>
    public static AutomationElement? WaitForElementByName(AutomationElement parent, string name, int maxSeconds = DefaultTimeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed.TotalSeconds < maxSeconds)
        {
            try
            {
                var element = parent.FindFirstDescendant(cf => cf.ByName(name));
                
                if (element != null && element.IsAvailable)
                {
                    return element;
                }
            }
            catch { }

            Thread.Sleep(RetryIntervalMs);
        }

        return null;
    }

    /// <summary>
    /// Capture categorized screenshot on failure
    /// </summary>
    public static string CaptureFailureScreenshot(Window? window, string testName, FailureType failureType, Exception ex)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"{testName}_{failureType}_{timestamp}.png";
        var fullPath = Path.Combine(ArtifactsPath, filename);

        try
        {
            Directory.CreateDirectory(ArtifactsPath);

            if (window != null && window.IsAvailable)
            {
                var image = Capture.Element(window);
                image.ToFile(fullPath);
            }
            else
            {
                var image = Capture.Screen();
                image.ToFile(fullPath);
            }

            Console.WriteLine($"[SCREENSHOT:{failureType}] {fullPath}");
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        catch (Exception captureEx)
        {
            Console.WriteLine($"[WARNING] Screenshot failed: {captureEx.Message}");
            fullPath = string.Empty;
        }

        return fullPath;
    }

    /// <summary>
    /// Capture success screenshot
    /// </summary>
    public static void CaptureSuccessScreenshot(Window window, string testName)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"{testName}_SUCCESS_{timestamp}.png";
        var fullPath = Path.Combine(ArtifactsPath, filename);

        try
        {
            Directory.CreateDirectory(ArtifactsPath);
            
            if (window.IsAvailable)
            {
                var image = Capture.Element(window);
                image.ToFile(fullPath);
                Console.WriteLine($"[SCREENSHOT:SUCCESS] {fullPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Screenshot failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely close application
    /// </summary>
    public static void CloseApp(FlaUIApp? app, UIA3Automation? automation)
    {
        try { app?.Close(); app?.Dispose(); }
        catch { try { app?.Kill(); } catch { } }
        
        automation?.Dispose();
    }

    /// <summary>
    /// Classify exception to failure type
    /// </summary>
    public static FailureType ClassifyException(Exception ex)
    {
        return ex switch
        {
            TimeoutException => FailureType.Timeout,
            Xunit.Sdk.XunitException => FailureType.Assertion,
            InvalidOperationException when ex.Message.Contains("not available") => FailureType.Crash,
            _ when ex.Message.Contains("timeout") || ex.Message.Contains("Timeout") => FailureType.Timeout,
            _ when ex.Message.Contains("crash") || ex.Message.Contains("Crash") => FailureType.Crash,
            _ => FailureType.UI
        };
    }
}
