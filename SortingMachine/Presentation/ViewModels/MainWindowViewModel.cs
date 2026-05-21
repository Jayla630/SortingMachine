// =========================================================
// File: Presentation/ViewModels/MainWindowViewModel.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
using Microsoft.Extensions.Logging;
using Prism.Mvvm;

namespace SortingMachine.Presentation.ViewModels;

public class MainWindowViewModel : BindableBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private string _statusText = "系统就绪";

    // TODO: Sprint S1 (ClaudeCode) - 注入 IMotionController
    public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
    {
        _logger = logger;
        _logger.LogInformation("MainWindowViewModel initialized");
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string Title => "高精度锂电池模组视觉贴片与分选系统 v1.0";
}
