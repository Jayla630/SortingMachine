// =========================================================
// File: Presentation/ViewModels/SortingMonitorViewModel.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Gemini CLI
// =========================================================
using Prism.Commands;
using Prism.Mvvm;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace SortingMachine.Presentation.ViewModels;

// 最近记录的展示数据类（不是领域模型）
public record SortingLogEntry(
    string CellId,
    string Grade,
    string BinId,
    bool IsSuccess,
    string DurationText,      // "1.23s"
    DateTime SortedAt);

public class SortingMonitorViewModel : BindableBase
{
    private readonly ISortingService _sortingService;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ILogger<SortingMonitorViewModel> _logger;

    // ── 状态 backing fields ──────────────────────────────────
    private string _sortingStatus = "空闲";
    public string SortingStatus
    {
        get => _sortingStatus;
        set => SetProperty(ref _sortingStatus, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RaiseCommandsCanExecuteChanged();
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandsCanExecuteChanged();
            }
        }
    }

    private int _totalSortedCount;
    public int TotalSortedCount
    {
        get => _totalSortedCount;
        set => SetProperty(ref _totalSortedCount, value);
    }

    private string _activeRecipeName = "未加载配方";
    public string ActiveRecipeName
    {
        get => _activeRecipeName;
        set => SetProperty(ref _activeRecipeName, value);
    }

    // ── 等级统计 backing fields ──────────────────────────────
    private int _gradeACount;
    public int GradeACount
    {
        get => _gradeACount;
        set => SetProperty(ref _gradeACount, value);
    }

    private int _gradeBCount;
    public int GradeBCount
    {
        get => _gradeBCount;
        set => SetProperty(ref _gradeBCount, value);
    }

    private int _gradeCCount;
    public int GradeCCount
    {
        get => _gradeCCount;
        set => SetProperty(ref _gradeCCount, value);
    }

    private int _gradeNgCount;
    public int GradeNgCount
    {
        get => _gradeNgCount;
        set => SetProperty(ref _gradeNgCount, value);
    }

    // ── 手动触发分选 backing fields ──────────────────────────
    private double _manualOcv = 3800;
    public double ManualOcv
    {
        get => _manualOcv;
        set => SetProperty(ref _manualOcv, value);
    }

    private double _manualIr = 15;
    public double ManualIr
    {
        get => _manualIr;
        set => SetProperty(ref _manualIr, value);
    }

    private string _manualCellId = "TEST-001";
    public string ManualCellId
    {
        get => _manualCellId;
        set => SetProperty(ref _manualCellId, value);
    }

    // ── 集合 ──────────────────────────────────────────
    public ObservableCollection<BinStatusViewModel> Bins { get; } = new();
    public ObservableCollection<SortingLogEntry> RecentLogs { get; } = new();

    // ── 命令 ──────────────────────────────────────────
    public DelegateCommand StartSortingCommand { get; }    // 启动自动分选
    public DelegateCommand StopSortingCommand { get; }     // 停止自动分选
    public DelegateCommand ManualSortCommand { get; }      // 手动触发一次分选
    public DelegateCommand ResetCountsCommand { get; }     // 重置料仓计数

    // 构造函数注入
    public SortingMonitorViewModel(
        ISortingService sortingService,
        IRecipeRepository recipeRepository,
        ILogger<SortingMonitorViewModel> logger)
    {
        _sortingService = sortingService;
        _recipeRepository = recipeRepository;
        _logger = logger;

        StartSortingCommand = new DelegateCommand(ExecuteStartSorting, () => !IsBusy && !IsRunning);
        StopSortingCommand = new DelegateCommand(ExecuteStopSorting, () => !IsBusy && IsRunning);
        ManualSortCommand = new DelegateCommand(async () => await ExecuteManualSortAsync(), () => !IsBusy);
        ResetCountsCommand = new DelegateCommand(ExecuteResetCounts, () => !IsBusy);

        _sortingService.SortingCompleted += OnSortingCompleted;
        _sortingService.BinFull += OnBinFull;

        // 异步初始化激活的配方
        _ = InitializeActiveRecipeAsync();
    }

    private async Task InitializeActiveRecipeAsync()
    {
        try
        {
            var active = _sortingService.ActiveRecipe;
            if (active == null)
            {
                var dbActive = await _recipeRepository.GetActiveAsync();
                if (dbActive != null)
                {
                    _sortingService.LoadRecipe(dbActive);
                    active = dbActive;
                }
            }

            if (active != null)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    RefreshBinStatus();
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load active recipe on SortingMonitorViewModel startup");
        }
    }

    private void ExecuteStartSorting()
    {
        if (_sortingService.ActiveRecipe == null)
        {
            SortingStatus = "请先在配方管理页激活配方";
            return;
        }
        IsRunning = true;
        SortingStatus = "运行中";
    }

    private void ExecuteStopSorting()
    {
        IsRunning = false;
        SortingStatus = "已暂停";
    }

    private void ExecuteResetCounts()
    {
        IsBusy = true;
        try
        {
            _sortingService.ResetBinCounts();
            TotalSortedCount = 0;
            GradeACount = 0;
            GradeBCount = 0;
            GradeCCount = 0;
            GradeNgCount = 0;
            RecentLogs.Clear();
            RefreshBinStatus();
            SortingStatus = "计数已重置";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset counts");
            SortingStatus = "重置失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteManualSortAsync()
    {
        if (_sortingService.ActiveRecipe == null)
        {
            SortingStatus = "请先在配方管理页激活配方";
            return;
        }

        IsBusy = true;
        SortingStatus = $"分选中 {ManualCellId}...";

        try
        {
            var measurement = new CellMeasurement
            {
                CellId = ManualCellId,
                OcvVoltage = ManualOcv,
                IrResistance = ManualIr,
                MeasuredAt = DateTime.Now
            };

            var result = await _sortingService.SortCellAsync(measurement);
            SortingStatus = result.IsSuccess
                ? $"完成 {result.CellId} → {result.Grade} → {result.BinId}"
                : $"失败: {result.ErrorMessage}";

            ManualCellId = IncrementCellId(ManualCellId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual sorting failed");
            SortingStatus = $"异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSortingCompleted(object? sender, SortingCompletedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            TotalSortedCount++;

            // 更新等级统计
            switch (e.Result.Grade)
            {
                case CellGrade.A: GradeACount++; break;
                case CellGrade.B: GradeBCount++; break;
                case CellGrade.C: GradeCCount++; break;
                case CellGrade.NG: GradeNgCount++; break;
            }

            // 更新料仓容量（从 ActiveRecipe 的 Bins 刷新）
            RefreshBinStatus();

            // 最近记录（保留最新 50 条，超出则删除最旧的）
            var entry = new SortingLogEntry(
                e.Result.CellId,
                e.Result.Grade.ToString(),
                e.Result.BinId ?? "—",
                e.Result.IsSuccess,
                $"{e.Result.Duration.TotalSeconds:F2}s",
                e.Result.SortedAt);

            RecentLogs.Insert(0, entry);
            while (RecentLogs.Count > 50)
                RecentLogs.RemoveAt(RecentLogs.Count - 1);
        });
    }

    private void OnBinFull(object? sender, BinFullEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            SortingStatus = $"报警: 料仓 {e.Bin.BinId} 已满！";
            _logger.LogWarning("料仓 {BinId} 已满，等级: {Grade}", e.Bin.BinId, e.Grade);
        });
    }

    private void RefreshBinStatus()
    {
        var recipe = _sortingService.ActiveRecipe;
        if (recipe == null) return;

        // 如果 Bins 集合为空则初始化
        if (Bins.Count == 0)
        {
            foreach (var bin in recipe.Bins)
            {
                Bins.Add(new BinStatusViewModel
                {
                    BinId = bin.BinId,
                    GradeLabel = $"{bin.TargetGrade} 级",
                    CurrentCount = bin.CurrentCount,
                    MaxCapacity = bin.MaxCapacity
                });
            }
            ActiveRecipeName = recipe.ProductModel;
            return;
        }

        // 已有则只更新计数
        foreach (var binVm in Bins)
        {
            var bin = recipe.Bins.FirstOrDefault(b => b.BinId == binVm.BinId);
            if (bin != null) binVm.UpdateFromBin(bin);
        }
        ActiveRecipeName = recipe.ProductModel;
    }

    private void RaiseCommandsCanExecuteChanged()
    {
        StartSortingCommand.RaiseCanExecuteChanged();
        StopSortingCommand.RaiseCanExecuteChanged();
        ManualSortCommand.RaiseCanExecuteChanged();
        ResetCountsCommand.RaiseCanExecuteChanged();
    }

    private string IncrementCellId(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return "TEST-001";
        var match = Regex.Match(cellId, @"^(.*?)(\d+)$");
        if (match.Success)
        {
            var prefix = match.Groups[1].Value;
            var numStr = match.Groups[2].Value;
            if (long.TryParse(numStr, out var number))
            {
                number++;
                return prefix + number.ToString(new string('0', numStr.Length));
            }
        }
        return cellId + "-1";
    }
}
