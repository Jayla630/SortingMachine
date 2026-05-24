// =========================================================
// File: App.xaml.cs
// Project: SortingMachine
// Sprint: S3-Hotfix | Agent: Claude Code
// =========================================================
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using SortingMachine.Application;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.IO;
using SortingMachine.Infrastructure.Mes;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Infrastructure.Persistence;
using SortingMachine.Presentation.Views;

namespace SortingMachine;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    => Container.Resolve<SortingMachine.Presentation.Views.MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // ── Motion (S1) ──────────────────────────────────────
        var mockController = new MockMotionController();
        containerRegistry.RegisterInstance<IMotionController>(mockController);
        containerRegistry.RegisterSingleton<IDigitalIO, MockDigitalIO>();

        // ── Safety & Homing (S2) ─────────────────────────────
        containerRegistry.RegisterSingleton<ISafetyValidator, MotionSafetyValidator>();
        containerRegistry.RegisterSingleton<IHomingStateMachine, HomingStateMachine>();

        // ── Sorting Service (S3) ─────────────────────────────
        containerRegistry.RegisterSingleton<ISortingService, SortingService>();

        // ── Logging (Serilog) ────────────────────────────────
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SortingMachine")
            .CreateLogger();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
        containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));

        Log.Information("=== 锂电池检测分选机控制系统启动 ===");
        Log.Information("版本：v1.0 | 模式：仿真（MockMotionController）");

        // ── FreeSql + Recipe (S3) ────────────────────────────
        try
        {
            var fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(FreeSql.DataType.Sqlite,
                    "Data Source=sorting_machine.db; Pooling=true; Min Pool Size=1")
                .UseAutoSyncStructure(false)
                .Build();

            containerRegistry.RegisterInstance<IFreeSql>(fsql);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] FreeSql 初始化失败: {ex.Message}");
            throw;
        }

        containerRegistry.RegisterSingleton<IRecipeRepository, RecipeRepository>();
        containerRegistry.RegisterSingleton<ISortingLogRepository, SortingLogRepository>();
        containerRegistry.RegisterSingleton<DatabaseInitializer>();
        containerRegistry.RegisterSingleton<IMesUploader, MockMesUploadService>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<AppModule>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== 系统正常退出 ===");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
