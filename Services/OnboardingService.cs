namespace NetDiagPro.Services;

/// <summary>
/// 新手引导服务 - 管理首次使用引导流程
/// </summary>
public class OnboardingService
{
    private const string OnboardingCompletedKey = "OnboardingCompleted";
    private const string OnboardingVersionKey = "OnboardingVersion";
    private const int CurrentOnboardingVersion = 1;

    private readonly Windows.Storage.ApplicationDataContainer _localSettings;

    public OnboardingService()
    {
        _localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
    }

    /// <summary>
    /// 检查是否需要显示引导
    /// </summary>
    public bool ShouldShowOnboarding()
    {
        var completed = _localSettings.Values[OnboardingCompletedKey] as bool? ?? false;
        var version = _localSettings.Values[OnboardingVersionKey] as int? ?? 0;

        // 如果未完成或版本过旧，显示引导
        return !completed || version < CurrentOnboardingVersion;
    }

    /// <summary>
    /// 标记引导已完成
    /// </summary>
    public void MarkOnboardingCompleted()
    {
        _localSettings.Values[OnboardingCompletedKey] = true;
        _localSettings.Values[OnboardingVersionKey] = CurrentOnboardingVersion;
    }

    /// <summary>
    /// 重置引导状态（用于测试）
    /// </summary>
    public void ResetOnboarding()
    {
        _localSettings.Values[OnboardingCompletedKey] = false;
        _localSettings.Values[OnboardingVersionKey] = 0;
    }

    /// <summary>
    /// 获取引导步骤
    /// </summary>
    public static List<OnboardingStep> GetOnboardingSteps()
    {
        return new List<OnboardingStep>
        {
            new OnboardingStep
            {
                Title = "欢迎使用 NetDiag Pro 👋",
                Description = "智能网络诊断管理平台，帮你快速发现和解决网络问题。",
                Icon = "\uE8D7",
                TargetPage = "Dashboard"
            },
            new OnboardingStep
            {
                Title = "一键测速 🚀",
                Description = "点击「带宽测速」即可测试下载/上传速度和网络延迟。",
                Icon = "\uE896",
                TargetPage = "SpeedTest"
            },
            new OnboardingStep
            {
                Title = "网络优化 🔧",
                Description = "DNS 优化、网络重置、健康评分，一站式优化网络体验。",
                Icon = "\uE90F",
                TargetPage = "NetworkOptimize"
            },
            new OnboardingStep
            {
                Title = "AI 智能助手 🤖",
                Description = "用自然语言描述问题，AI 帮你诊断并给出建议。",
                Icon = "\uE99A",
                TargetPage = "AIAssistant"
            },
            new OnboardingStep
            {
                Title = "开始使用！ ✨",
                Description = "点击左侧导航栏探索更多功能，随时可以在设置中再次查看引导。",
                Icon = "\uE73E",
                TargetPage = ""
            }
        };
    }
}

/// <summary>
/// 引导步骤数据
/// </summary>
public class OnboardingStep
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string TargetPage { get; set; } = "";
}
