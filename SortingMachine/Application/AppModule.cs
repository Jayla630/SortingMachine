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
using SortingMachine.Domain;
using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Application;

public class AppModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        // 1. 初始化运动控制器（必须第一步）
        var motionController = containerProvider.Resolve<IMotionController>();
        motionController.InitializeAsync().GetAwaiter().GetResult();

        // 2. 初始化数据库（建表 + 种子数据）
        var dbInit = containerProvider.Resolve<DatabaseInitializer>();
        dbInit.InitializeAsync().GetAwaiter().GetResult();
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 注册已移至 App.xaml.cs，此处留空
    }
}
