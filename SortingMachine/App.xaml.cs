// =========================================================
// File: App.xaml.cs
// Project: SortingMachine
// Sprint: S3-Hotfix | Agent: Claude Code
// =========================================================
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using SortingMachine.Application;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.IO;
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
        containerRegistry.RegisterSingleton<IMotionController, MockMotionController>();
        containerRegistry.RegisterSingleton<IDigitalIO, MockDigitalIO>();

        // ── Safety & Homing (S2) ─────────────────────────────
        containerRegistry.RegisterSingleton<ISafetyValidator, MotionSafetyValidator>();
        containerRegistry.RegisterSingleton<IHomingStateMachine, HomingStateMachine>();

        // ── Sorting Service (S3) ─────────────────────────────
        containerRegistry.RegisterSingleton<ISortingService, SortingService>();

        // ── Logging ──────────────────────────────────────────
        containerRegistry.Register(typeof(ILogger<>), typeof(NullLogger<>));

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
        containerRegistry.RegisterSingleton<DatabaseInitializer>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<AppModule>();
    }
}
