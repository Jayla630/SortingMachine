// =========================================================
// File: Presentation/ViewModels/BinStatusViewModel.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Gemini CLI
// =========================================================
using Prism.Mvvm;
using SortingMachine.Domain.Recipe;
using System;

namespace SortingMachine.Presentation.ViewModels;

public class BinStatusViewModel : BindableBase
{
    private string _binId = string.Empty;
    public string BinId
    {
        get => _binId;
        set => SetProperty(ref _binId, value);
    }

    private string _gradeLabel = string.Empty;
    public string GradeLabel
    {
        get => _gradeLabel;
        set => SetProperty(ref _gradeLabel, value);
    }

    private int _currentCount;
    public int CurrentCount
    {
        get => _currentCount;
        set
        {
            if (SetProperty(ref _currentCount, value))
            {
                RaisePropertyChanged(nameof(FillPercent));
                RaisePropertyChanged(nameof(IsFull));
                RaisePropertyChanged(nameof(CountLabel));
                RaisePropertyChanged(nameof(StatusColor));
            }
        }
    }

    private int _maxCapacity;
    public int MaxCapacity
    {
        get => _maxCapacity;
        set
        {
            if (SetProperty(ref _maxCapacity, value))
            {
                RaisePropertyChanged(nameof(FillPercent));
                RaisePropertyChanged(nameof(IsFull));
                RaisePropertyChanged(nameof(CountLabel));
                RaisePropertyChanged(nameof(StatusColor));
            }
        }
    }

    public double FillPercent => MaxCapacity > 0
        ? Math.Min(100.0 * CurrentCount / MaxCapacity, 100.0)
        : 0;

    public bool IsFull => CurrentCount >= MaxCapacity;

    public string CountLabel => $"{CurrentCount} / {MaxCapacity}";

    // 颜色：< 80% 绿色，80~95% 黄色，>= 95% 红色
    public string StatusColor => FillPercent >= 95 ? "#E53935"
                               : FillPercent >= 80 ? "#F9A825"
                               : "#43A047";

    public void UpdateFromBin(BinDefinition bin)
    {
        CurrentCount = bin.CurrentCount;
        MaxCapacity = bin.MaxCapacity;
        RaisePropertyChanged(nameof(CurrentCount));
        RaisePropertyChanged(nameof(FillPercent));
        RaisePropertyChanged(nameof(IsFull));
        RaisePropertyChanged(nameof(CountLabel));
        RaisePropertyChanged(nameof(StatusColor));
    }
}
