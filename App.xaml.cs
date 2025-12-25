using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using NetDiagPro.Services;
using System.Diagnostics;

namespace NetDiagPro;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    
    /// <summary>
    /// 全局服务提供者 (依赖注入容器)
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
        
        try
        {
            // 配置依赖注入
            Services = ConfigureServices();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DI Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 配置服务注册
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 注册网络诊断服务 (Singleton - 全局共享)
        services.AddSingleton<SpeedTestService>();
        services.AddSingleton<NetworkTesterService>();
        services.AddSingleton<TracerouteService>();
        services.AddSingleton<IPDetectorService>();
        services.AddSingleton<LANScannerService>();
        services.AddSingleton<StreamingTesterService>();
        
        // 注册优化服务
        services.AddSingleton<NetworkOptimizerService>();
        services.AddSingleton<TrafficMonitorService>();
        services.AddSingleton<WifiAnalyzerService>();
        
        // 注册数据服务
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<ReportExportService>();
        
        // 注册用户引导服务
        services.AddSingleton<OnboardingService>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 获取服务实例的便捷方法
    /// </summary>
    public static T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Launch Error: {ex.Message}");
        }
    }
}
