// =========================================================
// File: Application/AppModule.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Gemini CLI
// =========================================================
using Prism.Ioc;
using Prism.Modularity;
using SortingMachine.Presentation.Views;
using SortingMachine.Presentation.ViewModels;
using SortingMachine.Domain.Recipe;
using SortingMachine.Infrastructure.Persistence;
using FreeSql;

namespace SortingMachine.Application;

public class AppModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        // TODO: Sprint S3 - 初始化硬件通讯

        // 初始化数据库（建表 + 种子数据）
        // TODO: Prism 9 模块异步初始化方案待优化
        var dbInit = containerProvider.Resolve<DatabaseInitializer>();
        dbInit.InitializeAsync().GetAwaiter().GetResult();
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // FreeSql 配置（SQLite，数据库文件在运行目录）
        var fsql = new FreeSql.FreeSqlBuilder()
            .UseConnectionString(FreeSql.DataType.Sqlite,
                "Data Source=sorting_machine.db; Pooling=true; Min Pool Size=1")
            .UseAutoSyncStructure(false)   // 手动控制建表时机
            .Build();

        containerRegistry.RegisterInstance<IFreeSql>(fsql);
        containerRegistry.RegisterSingleton<IRecipeRepository, RecipeRepository>();
        containerRegistry.RegisterSingleton<DatabaseInitializer>();

        // ISortingService 注册（等 Claude Code 完成后取消注释）
        // containerRegistry.RegisterSingleton<ISortingService, SortingService>();

        // 注册模块内 DI 映射 (随 Sprint 进度增加)
        containerRegistry.RegisterForNavigation<MotionDebugView, MotionDebugViewModel>();
        containerRegistry.RegisterForNavigation<RecipeView, RecipeViewModel>();
    }
}
