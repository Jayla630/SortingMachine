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
        // 注册已移至 App.xaml.cs，此处留空
    }
}
