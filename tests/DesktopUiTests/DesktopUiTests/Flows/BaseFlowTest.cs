using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using DesktopUiTests.Pages;
using DesktopUiTests.Utilities;
using FlaUIApp = FlaUI.Core.Application;

namespace DesktopUiTests.Flows;

/// <summary>
/// Base class for all Flow Tests
/// </summary>
public abstract class BaseFlowTest : IDisposable
{
    protected FlaUIApp? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected MainWindowPage? MainWindowPage { get; private set; }

    /// <summary>
    /// Launch application and prepare for testing
    /// </summary>
    protected void LaunchAndPrepare()
    {
        (App, Automation) = TestHelper.LaunchApp();
        MainWindow = TestHelper.WaitForMainWindow(App, Automation);
        
        Assert.NotNull(MainWindow);
        MainWindowPage = new MainWindowPage(MainWindow);
    }

    /// <summary>
    /// Execute test action with automatic failure handling
    /// </summary>
    protected void ExecuteWithFailureHandling(string testName, Action testAction)
    {
        try
        {
            LaunchAndPrepare();
            testAction();
            
            // Success screenshot
            if (MainWindow != null)
            {
                TestHelper.CaptureSuccessScreenshot(MainWindow, testName);
            }
        }
        catch (Exception ex)
        {
            var failureType = TestHelper.ClassifyException(ex);
            TestHelper.CaptureFailureScreenshot(MainWindow, testName, failureType, ex);
            throw;
        }
    }

    public void Dispose()
    {
        TestHelper.CloseApp(App, Automation);
        GC.SuppressFinalize(this);
    }
}
