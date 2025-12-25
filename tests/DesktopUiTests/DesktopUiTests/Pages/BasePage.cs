using FlaUI.Core.AutomationElements;
using DesktopUiTests.Utilities;

namespace DesktopUiTests.Pages;

/// <summary>
/// Base class for all Page Objects
/// </summary>
public abstract class BasePage
{
    protected Window MainWindow { get; }

    protected BasePage(Window mainWindow)
    {
        MainWindow = mainWindow;
    }

    /// <summary>
    /// Find element by AutomationId on current page
    /// </summary>
    public AutomationElement? FindById(string automationId, int timeoutSeconds = 5)
    {
        return TestHelper.WaitForElement(MainWindow, automationId, timeoutSeconds);
    }

    /// <summary>
    /// Find element by Name on current page
    /// </summary>
    public AutomationElement? FindByName(string name, int timeoutSeconds = 5)
    {
        return TestHelper.WaitForElementByName(MainWindow, name, timeoutSeconds);
    }

    /// <summary>
    /// Find and click element by AutomationId
    /// </summary>
    public bool ClickById(string automationId, int timeoutSeconds = 5)
    {
        var element = FindById(automationId, timeoutSeconds);
        if (element != null && element.IsAvailable)
        {
            element.Click();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Find and click element by Name
    /// </summary>
    public bool ClickByName(string name, int timeoutSeconds = 5)
    {
        var element = FindByName(name, timeoutSeconds);
        if (element != null && element.IsAvailable)
        {
            element.Click();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if page/element is available
    /// </summary>
    public bool IsAvailable => MainWindow.IsAvailable;
}
