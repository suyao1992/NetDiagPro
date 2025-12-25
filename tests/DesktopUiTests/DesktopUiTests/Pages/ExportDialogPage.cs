using FlaUI.Core.AutomationElements;

namespace DesktopUiTests.Pages;

/// <summary>
/// Page Object for Export Dialog
/// </summary>
public class ExportDialogPage : BasePage
{
    // AutomationIds
    public const string FormatHtml = "ExportFormatHtml";
    public const string FormatCsv = "ExportFormatCsv";
    public const string FormatJson = "ExportFormatJson";
    public const string ExportButton = "ExportBtn";
    public const string CancelButton = "CancelExportBtn";
    public const string StatusBar = "ExportStatusBar";

    public ExportDialogPage(Window mainWindow) : base(mainWindow) { }

    /// <summary>
    /// Select HTML format
    /// </summary>
    public bool SelectHtmlFormat()
    {
        return ClickById(FormatHtml, 3) || ClickByName("HTML 报告", 3);
    }

    /// <summary>
    /// Select CSV format
    /// </summary>
    public bool SelectCsvFormat()
    {
        return ClickById(FormatCsv, 3) || ClickByName("CSV 表格", 3);
    }

    /// <summary>
    /// Select JSON format
    /// </summary>
    public bool SelectJsonFormat()
    {
        return ClickById(FormatJson, 3) || ClickByName("JSON 数据", 3);
    }

    /// <summary>
    /// Click Export button
    /// </summary>
    public bool ClickExport()
    {
        return ClickById(ExportButton, 3) || ClickByName("导出", 3);
    }

    /// <summary>
    /// Click Cancel button
    /// </summary>
    public bool ClickCancel()
    {
        return ClickById(CancelButton, 3) || ClickByName("取消", 3);
    }

    /// <summary>
    /// Wait for export status message
    /// </summary>
    public bool WaitForExportComplete(int maxSeconds = 30)
    {
        var start = DateTime.Now;
        
        while ((DateTime.Now - start).TotalSeconds < maxSeconds)
        {
            var status = FindById(StatusBar, 1);
            if (status != null && status.IsAvailable)
            {
                var text = status.Name ?? "";
                if (text.Contains("成功") || text.Contains("已导出") || text.Contains("Success"))
                {
                    return true;
                }
            }
            
            Thread.Sleep(500);
        }
        
        return false;
    }

    /// <summary>
    /// Check if dialog is open
    /// </summary>
    public bool IsOpen()
    {
        var exportBtn = FindById(ExportButton, 3) ?? FindByName("导出", 3);
        return exportBtn != null && exportBtn.IsAvailable;
    }
}
