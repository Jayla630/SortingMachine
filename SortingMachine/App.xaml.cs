// =========================================================
// File: App.xaml.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using SortingMachine.Application;
using SortingMachine.Infrastructure.IO;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Presentation.Views;

namespace SortingMachine;

public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // TODO: Sprint S1 - Claude Code 负责生成 IMotionController
        containerRegistry.RegisterSingleton<IMotionController, MockMotionController>();

        containerRegistry.RegisterSingleton<IDigitalIO, MockDigitalIO>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<AppModule>();
    }
}
