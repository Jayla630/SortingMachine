// =========================================================
// File: Presentation/ViewModels/MotionDebugViewModel.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Gemini CLI
// =========================================================
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using SortingMachine.Infrastructure.Motion;
using System.Windows;

namespace SortingMachine.Presentation.ViewModels;

public class MotionDebugViewModel : BindableBase
{
    private readonly IMotionController _motionController;
    private readonly ILogger<MotionDebugViewModel> _logger;
    private string _pageStatus = "就绪";
    private bool _isEmergencyStopped;
    private bool _isBusy;

    public MotionDebugViewModel(
        IMotionController motionController,
        ILogger<MotionDebugViewModel> logger)
    {
        _motionController = motionController;
        _logger = logger;

        AxisX = new AxisPanelViewModel(AxisId.X, "X 轴");
        AxisY = new AxisPanelViewModel(AxisId.Y, "Y 轴");
        AxisZ = new AxisPanelViewModel(AxisId.Z, "Z 轴");

        JogPositiveCommand = new DelegateCommand<object>(async (axis) => await ExecuteJogAsync(axis, true), CanExecuteMotion);
        JogNegativeCommand = new DelegateCommand<object>(async (axis) => await ExecuteJogAsync(axis, false), CanExecuteMotion);
        StopAxisCommand = new DelegateCommand<object>(async (axis) => await ExecuteStopAsync(axis));
        MoveAbsoluteCommand = new DelegateCommand<object>(async (axis) => await ExecuteMoveAbsoluteAsync(axis), CanExecuteMotion);
        HomeAxisCommand = new DelegateCommand<object>(async (axis) => await ExecuteHomeAsync(axis), CanExecuteMotion);
        HomeAllAxesCommand = new DelegateCommand(async () => await ExecuteHomeAllAxesAsync(), CanExecuteMotion);
        EmergencyStopCommand = new DelegateCommand(async () => await ExecuteEmergencyStopAsync());
        ToggleEnableCommand = new DelegateCommand<object>(async (axis) => await ExecuteToggleEnableAsync(axis), CanExecuteMotion);
        ClearAlarmCommand = new DelegateCommand<object>(async (axis) => await ExecuteClearAlarmAsync(axis), CanExecuteMotion);
        ClearAllAlarmsCommand = new DelegateCommand(async () => await ExecuteClearAllAlarmsAsync(), CanExecuteMotion);

        _motionController.AxisStatusChanged += OnAxisStatusChanged;
        _motionController.AlarmOccurred += OnAlarmOccurred;
    }

    public AxisPanelViewModel AxisX { get; }
    public AxisPanelViewModel AxisY { get; }
    public AxisPanelViewModel AxisZ { get; }

    public string PageStatus
    {
        get => _pageStatus;
        set => SetProperty(ref _pageStatus, value);
    }

    public bool IsEmergencyStopped
    {
        get => _isEmergencyStopped;
        set => SetProperty(ref _isEmergencyStopped, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public DelegateCommand<object> JogPositiveCommand { get; }
    public DelegateCommand<object> JogNegativeCommand { get; }
    public DelegateCommand<object> StopAxisCommand { get; }
    public DelegateCommand<object> MoveAbsoluteCommand { get; }
    public DelegateCommand<object> HomeAxisCommand { get; }
    public DelegateCommand HomeAllAxesCommand { get; }
    public DelegateCommand EmergencyStopCommand { get; }
    public DelegateCommand<object> ToggleEnableCommand { get; }
    public DelegateCommand<object> ClearAlarmCommand { get; }
    public DelegateCommand ClearAllAlarmsCommand { get; }

    private bool CanExecuteMotion(object? axis) => !IsBusy;
    private bool CanExecuteMotion() => !IsBusy;

    private void RaiseCommandCanExecuteChanged()
    {
        JogPositiveCommand.RaiseCanExecuteChanged();
        JogNegativeCommand.RaiseCanExecuteChanged();
        MoveAbsoluteCommand.RaiseCanExecuteChanged();
        HomeAxisCommand.RaiseCanExecuteChanged();
        HomeAllAxesCommand.RaiseCanExecuteChanged();
        ToggleEnableCommand.RaiseCanExecuteChanged();
        ClearAlarmCommand.RaiseCanExecuteChanged();
        ClearAllAlarmsCommand.RaiseCanExecuteChanged();
    }

    private async Task ExecuteJogAsync(object? axisIdObj, bool positive)
    {
        if (axisIdObj is not AxisId axisId) return;
        var panel = GetAxisPanel(axisId);
        try
        {
            IsBusy = true;
            PageStatus = $"正在点动 {axisId}...";
            var velocity = positive ? panel.JogVelocity : -panel.JogVelocity;
            var result = await _motionController.JogAsync(axisId, velocity);
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "点动失败";
                _logger.LogError("Jog failed for {AxisId}: {Error}", axisId, PageStatus);
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "Jog exception for {AxisId}", axisId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteStopAsync(object? axisIdObj)
    {
        if (axisIdObj is not AxisId axisId) return;
        try
        {
            PageStatus = $"正在停止 {axisId}...";
            var result = await _motionController.StopAsync(axisId);
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "停止失败";
                _logger.LogError("Stop failed for {AxisId}: {Error}", axisId, PageStatus);
            }
            else
            {
                PageStatus = "就绪";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "Stop exception for {AxisId}", axisId);
        }
    }

    private async Task ExecuteMoveAbsoluteAsync(object? axisIdObj)
    {
        if (axisIdObj is not AxisId axisId) return;
        var panel = GetAxisPanel(axisId);
        try
        {
            IsBusy = true;
            PageStatus = $"正在移动 {axisId} 到 {panel.TargetPosition}...";
            var result = await _motionController.MoveAbsoluteAsync(axisId, panel.TargetPosition, panel.MoveVelocity);
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "移动失败";
                _logger.LogError("MoveAbsolute failed for {AxisId}: {Error}", axisId, PageStatus);
            }
            else
            {
                PageStatus = "移动完成";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "MoveAbsolute exception for {AxisId}", axisId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteHomeAsync(object? axisIdObj)
    {
        if (axisIdObj is not AxisId axisId) return;
        try
        {
            IsBusy = true;
            PageStatus = $"正在回零 {axisId}...";
            var result = await _motionController.HomeAsync(axisId);
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "回零失败";
                _logger.LogError("Home failed for {AxisId}: {Error}", axisId, PageStatus);
            }
            else
            {
                PageStatus = "回零完成";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "Home exception for {AxisId}", axisId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteHomeAllAxesAsync()
    {
        try
        {
            IsBusy = true;
            PageStatus = "正在全轴回零...";
            var result = await _motionController.HomeAllAxesAsync();
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "全轴回零失败";
                _logger.LogError("HomeAllAxes failed: {Error}", PageStatus);
            }
            else
            {
                PageStatus = "全轴回零完成";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "HomeAllAxes exception");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteEmergencyStopAsync()
    {
        try
        {
            PageStatus = "!!! 紧急停止 !!!";
            var result = await _motionController.EmergencyStopAsync();
            if (!result.IsSuccess)
            {
                _logger.LogError("EmergencyStop failed: {Error}", result.ErrorMessage);
            }
            IsEmergencyStopped = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmergencyStop exception");
        }
    }

    private async Task ExecuteToggleEnableAsync(object? axisIdObj)
    {
        if (axisIdObj is not AxisId axisId) return;
        var panel = GetAxisPanel(axisId);
        try
        {
            IsBusy = true;
            MotionResult result;
            if (panel.IsEnabled)
            {
                PageStatus = $"正在禁用 {axisId}...";
                result = await _motionController.DisableAxisAsync(axisId);
            }
            else
            {
                PageStatus = $"正在使能 {axisId}...";
                result = await _motionController.EnableAxisAsync(axisId);
            }

            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "使能切换失败";
                _logger.LogError("ToggleEnable failed for {AxisId}: {Error}", axisId, PageStatus);
            }
            else
            {
                PageStatus = "使能切换成功";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "ToggleEnable exception for {AxisId}", axisId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteClearAlarmAsync(object? axisIdObj)
    {
        if (axisIdObj is not AxisId axisId) return;
        try
        {
            IsBusy = true;
            PageStatus = $"正在清除报警 {axisId}...";
            var result = await _motionController.ClearAlarmAsync(axisId);
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "清除报警失败";
                _logger.LogError("ClearAlarm failed for {AxisId}: {Error}", axisId, PageStatus);
            }
            else
            {
                PageStatus = "报警已清除";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "ClearAlarm exception for {AxisId}", axisId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteClearAllAlarmsAsync()
    {
        try
        {
            IsBusy = true;
            PageStatus = "正在清除全轴报警...";
            var result = await _motionController.ClearAllAlarmsAsync();
            if (!result.IsSuccess)
            {
                PageStatus = result.ErrorMessage ?? "清除全轴报警失败";
                _logger.LogError("ClearAllAlarms failed: {Error}", PageStatus);
            }
            else
            {
                PageStatus = "报警已全部清除";
            }
        }
        catch (Exception ex)
        {
            PageStatus = "操作异常";
            _logger.LogError(ex, "ClearAllAlarms exception");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnAxisStatusChanged(object? sender, AxisStatusChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var panel = GetAxisPanel(e.CurrentStatus.AxisId);
            panel.UpdateFromStatus(e.CurrentStatus);
        });
    }

    private void OnAlarmOccurred(object? sender, MotionAlarmEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            PageStatus = $"报警: {e.AxisId} - {e.AlarmMessage}";
            var panel = GetAxisPanel(e.AxisId);
            panel.HasAlarm = true;
            panel.AlarmMessage = e.AlarmMessage;
        });
    }

    private AxisPanelViewModel GetAxisPanel(AxisId axisId)
    {
        return axisId switch
        {
            AxisId.X => AxisX,
            AxisId.Y => AxisY,
            AxisId.Z => AxisZ,
            _ => throw new ArgumentException($"Invalid axis id: {axisId}")
        };
    }
}
