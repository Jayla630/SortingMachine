// =========================================================
// File: Presentation/ViewModels/AlarmLogViewModel.cs
// Project: SortingMachine
// Sprint: S5 | Agent: Gemini CLI
// =========================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using SortingMachine.Domain;

namespace SortingMachine.Presentation.ViewModels;

public class AlarmLogViewModel : BindableBase
{
    private readonly ISortingLogRepository _logRepository;
    private readonly IMesUploader _mesUploader;
    private readonly ILogger<AlarmLogViewModel> _logger;

    // ── 数据列表 ──────────────────────────────────────
    public ObservableCollection<SortingLog> FailedLogs { get; } = new();
    public ObservableCollection<SortingLog> PendingMesLogs { get; } = new();

    // ── 统计 backing fields ───────────────────────────
    private int _totalFailedCount;
    public int TotalFailedCount
    {
        get => _totalFailedCount;
        set => SetProperty(ref _totalFailedCount, value);
    }

    private int _pendingMesCount;
    public int PendingMesCount
    {
        get => _pendingMesCount;
        set => SetProperty(ref _pendingMesCount, value);
    }

    private string _mesEndpoint = string.Empty;
    public string MesEndpoint
    {
        get => _mesEndpoint;
        set => SetProperty(ref _mesEndpoint, value);
    }

    private bool _isMesConnected;
    public bool IsMesConnected
    {
        get => _isMesConnected;
        set => SetProperty(ref _isMesConnected, value);
    }

    // ── 状态 backing fields ───────────────────────────
    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
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

    // ── 筛选 backing fields ───────────────────────────
    private string _filterCellId = string.Empty;
    public string FilterCellId
    {
        get => _filterCellId;
        set => SetProperty(ref _filterCellId, value);
    }

    private DateTime _filterFrom = DateTime.Today;
    public DateTime FilterFrom
    {
        get => _filterFrom;
        set => SetProperty(ref _filterFrom, value);
    }

    private DateTime _filterTo = DateTime.Today.AddDays(1);
    public DateTime FilterTo
    {
        get => _filterTo;
        set => SetProperty(ref _filterTo, value);
    }

    // ── 命令 ──────────────────────────────────────────
    public DelegateCommand RefreshCommand { get; }           // 刷新列表
    public DelegateCommand UploadPendingCommand { get; }     // 批量上报待传 MES 记录
    public DelegateCommand<SortingLog> UploadSingleCommand { get; }  // 单条重传
    public DelegateCommand PingMesCommand { get; }           // 检测 MES 连通性
    public DelegateCommand ClearFilterCommand { get; }       // 清除筛选条件

    // 构造函数注入
    public AlarmLogViewModel(
        ISortingLogRepository logRepository,
        IMesUploader mesUploader,
        ILogger<AlarmLogViewModel> logger)
    {
        _logRepository = logRepository;
        _mesUploader = mesUploader;
        _logger = logger;

        RefreshCommand = new DelegateCommand(async () => await ExecuteRefreshAsync(), () => !IsBusy);
        UploadPendingCommand = new DelegateCommand(async () => await ExecuteUploadPendingAsync(), () => !IsBusy);
        UploadSingleCommand = new DelegateCommand<SortingLog>(async (log) => await ExecuteUploadSingleAsync(log), (log) => !IsBusy && log != null);
        PingMesCommand = new DelegateCommand(async () => await ExecutePingMesAsync(), () => !IsBusy);
        ClearFilterCommand = new DelegateCommand(ExecuteClearFilter, () => !IsBusy);

        // 自动初始化
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await ExecutePingMesAsync();
        await ExecuteRefreshAsync();
    }

    private async Task ExecuteRefreshAsync()
    {
        IsBusy = true;
        StatusText = "正在刷新...";
        try
        {
            // 1. 获取失败记录（IsSuccess = false）
            var logs = await _logRepository.GetByTimeRangeAsync(FilterFrom, FilterTo);
            var failedList = logs.Where(l => !l.IsSuccess);
            if (!string.IsNullOrWhiteSpace(FilterCellId))
            {
                failedList = failedList.Where(l => l.CellId.Contains(FilterCellId, StringComparison.OrdinalIgnoreCase));
            }

            FailedLogs.Clear();
            foreach (var log in failedList)
            {
                FailedLogs.Add(log);
            }
            TotalFailedCount = FailedLogs.Count;

            // 2. 获取待上报 MES 记录（IsSuccess = true && MesUploaded = false）
            var pendingList = await _logRepository.GetPendingMesUploadAsync();
            PendingMesLogs.Clear();
            foreach (var log in pendingList)
            {
                PendingMesLogs.Add(log);
            }
            PendingMesCount = PendingMesLogs.Count;

            // 3. 更新 Endpoint
            MesEndpoint = _mesUploader.Endpoint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新报警与上报记录失败");
            StatusText = $"刷新失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusText = "就绪";
        }
    }

    private async Task ExecuteUploadPendingAsync()
    {
        IsBusy = true;
        StatusText = "正在上报 MES...";
        try
        {
            var result = await _mesUploader.UploadPendingAsync();
            StatusText = result.IsSuccess
                ? $"上报成功：{result.SuccessCount} 条"
                : $"上报失败：{result.ErrorMessage}";
            await ExecuteRefreshAsync(); // 刷新列表
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量上报 MES 异常");
            StatusText = $"上报异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteUploadSingleAsync(SortingLog log)
    {
        if (log == null) return;
        IsBusy = true;
        StatusText = $"正在单条上报 {log.CellId}...";
        try
        {
            var result = await _mesUploader.UploadSingleAsync(log);
            StatusText = result.IsSuccess
                ? $"上报成功：{log.CellId}"
                : $"上报失败：{result.ErrorMessage}";
            await ExecuteRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "单条上报 MES 异常, CellId={CellId}", log.CellId);
            StatusText = $"上报异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecutePingMesAsync()
    {
        IsBusy = true;
        StatusText = "正在检测 MES 连通性...";
        try
        {
            IsMesConnected = await _mesUploader.PingAsync();
            StatusText = IsMesConnected ? "MES 连接成功" : "MES 连接失败";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检测 MES 连通性异常");
            IsMesConnected = false;
            StatusText = $"检测异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ExecuteClearFilter()
    {
        FilterCellId = string.Empty;
        FilterFrom = DateTime.Today;
        FilterTo = DateTime.Today.AddDays(1);
        _ = ExecuteRefreshAsync();
    }

    private void RaiseCommandsCanExecuteChanged()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        UploadPendingCommand.RaiseCanExecuteChanged();
        UploadSingleCommand.RaiseCanExecuteChanged();
        PingMesCommand.RaiseCanExecuteChanged();
        ClearFilterCommand.RaiseCanExecuteChanged();
    }
}
