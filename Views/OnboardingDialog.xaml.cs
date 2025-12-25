using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using NetDiagPro.Services;

namespace NetDiagPro.Views;

public sealed partial class OnboardingDialog : ContentDialog
{
    private readonly List<OnboardingStep> _steps;
    private readonly Ellipse[] _dots;
    private int _currentStep = 0;

    public event EventHandler<string>? NavigateToPageRequested;

    public OnboardingDialog()
    {
        this.InitializeComponent();
        _steps = OnboardingService.GetOnboardingSteps();
        _dots = new[] { Dot1, Dot2, Dot3, Dot4, Dot5 };
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_currentStep >= _steps.Count) return;

        var step = _steps[_currentStep];
        
        StepTitle.Text = step.Title;
        StepDescription.Text = step.Description;
        
        // Update icon
        try
        {
            StepIcon.Glyph = step.Icon;
        }
        catch { }

        // Update dots
        var accentBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var disabledBrush = (Brush)Application.Current.Resources["ControlStrongFillColorDisabledBrush"];

        for (int i = 0; i < _dots.Length; i++)
        {
            _dots[i].Fill = i == _currentStep ? accentBrush : disabledBrush;
        }

        // Update buttons
        if (_currentStep == _steps.Count - 1)
        {
            this.PrimaryButtonText = "开始使用";
            this.SecondaryButtonText = "";
        }
        else
        {
            this.PrimaryButtonText = "下一步";
            this.SecondaryButtonText = "跳过";
        }
    }

    private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_currentStep < _steps.Count - 1)
        {
            // Prevent dialog from closing
            args.Cancel = true;
            
            _currentStep++;
            UpdateUI();
        }
        else
        {
            // Last step, mark completed and close
            var onboarding = new OnboardingService();
            onboarding.MarkOnboardingCompleted();
        }
    }

    private void SecondaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Skip - mark completed
        var onboarding = new OnboardingService();
        onboarding.MarkOnboardingCompleted();
    }
}
