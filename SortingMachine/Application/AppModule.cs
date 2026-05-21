// =========================================================
// File: Application/AppModule.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
using Prism.Ioc;
using Prism.Modularity;

namespace SortingMachine.Application;

public class AppModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        // TODO: Sprint S3 - 初始化硬件通讯
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 注册模块内 DI 映射 (随 Sprint 进度增加)
    }
}
