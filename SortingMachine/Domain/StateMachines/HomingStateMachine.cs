// =========================================================
// File: Domain/StateMachines/HomingStateMachine.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Claude Code
// =========================================================

using Microsoft.Extensions.Logging;
using SortingMachine.Infrastructure.Motion;
using Stateless;

namespace SortingMachine.Domain.StateMachines;

/// <summary>
/// 回零状态机实现 —— 使用 Stateless 库管理三轴顺序回零流程。
/// </summary>
public sealed class HomingStateMachine : IHomingStateMachine
{
    private readonly IMotionController _motion;
    private readonly ISafetyValidator _safety;
    private readonly ILogger<HomingStateMachine> _logger;
    private readonly StateMachine<HomingState, HomingTrigger> _fsm;
    private readonly TimeSpan _axisHomingTimeout = TimeSpan.FromSeconds(30);

    private CancellationTokenSource? _cts;

    public HomingState CurrentState => _fsm.State;

    public event EventHandler<HomingStateChangedEventArgs>? StateChanged;

    public HomingStateMachine(
        IMotionController motion,
        ISafetyValidator safety,
        ILogger<HomingStateMachine> logger)
    {
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _fsm = new StateMachine<HomingState, HomingTrigger>(HomingState.Idle);

        ConfigureStateMachine();
    }

    // ── IHomingStateMachine ────────────────────────────────

    public async Task<HomingResult> ExecuteAsync(CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        // 幂等：已完成状态直接返回成功
        if (_fsm.State == HomingState.Completed)
        {
            _logger.LogInformation("Homing already completed, skipping");
            return HomingResult.Success(DateTime.UtcNow - startTime);
        }

        if (_fsm.State != HomingState.Idle)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogWarning("Cannot start homing: state is {State}, expected Idle", _fsm.State);
            return HomingResult.Failure(HomingFailureReason.PreConditionFailed,
                $"State machine is {_fsm.State}, call Reset() before retrying", duration);
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            // 1. 前置条件检查
            _fsm.Fire(HomingTrigger.Start);
            var preCheck = await RunPreCheckAsync(_cts.Token);
            if (!preCheck.IsSuccess)
            {
                _fsm.Fire(HomingTrigger.PreCheckFailed);
                _logger.LogError("Pre-condition check failed: {Error}", preCheck.ErrorMessage);
                return HomingResult.Failure(HomingFailureReason.PreConditionFailed,
                    preCheck.ErrorMessage!, DateTime.UtcNow - startTime);
            }

            _fsm.Fire(HomingTrigger.PreCheckOk);

            // 2. 顺序回零：Z → X → Y
            var zResult = await HomeSingleAxisAsync(AxisId.Z, HomingTrigger.ZHomed, _cts.Token);
            if (!zResult.IsSuccess)
                return BuildAxisFailure(zResult, startTime);

            var xResult = await HomeSingleAxisAsync(AxisId.X, HomingTrigger.XHomed, _cts.Token);
            if (!xResult.IsSuccess)
                return BuildAxisFailure(xResult, startTime);

            var yResult = await HomeSingleAxisAsync(AxisId.Y, HomingTrigger.YHomed, _cts.Token);
            if (!yResult.IsSuccess)
                return BuildAxisFailure(yResult, startTime);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("All axes homed successfully in {Duration}", duration);
            return HomingResult.Success(duration);
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.UtcNow - startTime;

            // 区分内部 CancelAsync 与外部 CancellationToken 取消
            if (_cts is { IsCancellationRequested: true } && !ct.IsCancellationRequested)
            {
                _fsm.Fire(HomingTrigger.Cancel);
                _logger.LogInformation("Homing cancelled by CancelAsync");
                return HomingResult.Cancel(duration);
            }

            _fsm.Fire(HomingTrigger.Cancel);
            _logger.LogInformation("Homing cancelled by external token");
            return HomingResult.Failure(HomingFailureReason.Cancelled,
                "Operation cancelled by external token", duration);
        }
    }

    public Task CancelAsync()
    {
        _logger.LogInformation("CancelAsync called, cancelling homing sequence");
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _logger.LogInformation("Resetting homing state machine from {State} to Idle", _fsm.State);
        _fsm.Fire(HomingTrigger.Reset);
        _cts?.Dispose();
        _cts = null;
    }

    // ── 状态机配置 ─────────────────────────────────────────

    private void ConfigureStateMachine()
    {
        _fsm.Configure(HomingState.Idle)
            .Permit(HomingTrigger.Start, HomingState.CheckingPreConditions);

        _fsm.Configure(HomingState.CheckingPreConditions)
            .Permit(HomingTrigger.PreCheckOk, HomingState.HomingZ)
            .Permit(HomingTrigger.PreCheckFailed, HomingState.Failed);

        _fsm.Configure(HomingState.HomingZ)
            .Permit(HomingTrigger.ZHomed, HomingState.HomingX)
            .Permit(HomingTrigger.AxisFailed, HomingState.Failed)
            .Permit(HomingTrigger.Cancel, HomingState.Cancelled);

        _fsm.Configure(HomingState.HomingX)
            .Permit(HomingTrigger.XHomed, HomingState.HomingY)
            .Permit(HomingTrigger.AxisFailed, HomingState.Failed)
            .Permit(HomingTrigger.Cancel, HomingState.Cancelled);

        _fsm.Configure(HomingState.HomingY)
            .Permit(HomingTrigger.YHomed, HomingState.Completed)
            .Permit(HomingTrigger.AxisFailed, HomingState.Failed)
            .Permit(HomingTrigger.Cancel, HomingState.Cancelled);

        _fsm.Configure(HomingState.Failed)
            .Permit(HomingTrigger.Reset, HomingState.Idle);

        _fsm.Configure(HomingState.Completed)
            .Permit(HomingTrigger.Reset, HomingState.Idle);

        _fsm.Configure(HomingState.Cancelled)
            .Permit(HomingTrigger.Reset, HomingState.Idle);

        // 每次状态转换触发事件
        _fsm.OnTransitioned(t =>
        {
            var args = new HomingStateChangedEventArgs(
                (HomingState)t.Source,
                (HomingState)t.Destination);
            OnStateChanged(args);
            _logger.LogDebug("Homing state: {Source} → {Destination}", t.Source, t.Destination);
        });
    }

    // ── 内部流程 ───────────────────────────────────────────

    private async Task<MotionResult> RunPreCheckAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running pre-condition checks");

        // 逐轴检查使能与报警状态
        foreach (var axis in new[] { AxisId.X, AxisId.Y, AxisId.Z })
        {
            ct.ThrowIfCancellationRequested();

            AxisStatus status;
            try
            {
                status = await _motion.GetAxisStatusAsync(axis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query axis {Axis} status — controller may not be initialized", axis);
                return MotionResult.Failure(MotionErrorCode.HardwareError,
                    $"Controller not ready: cannot query axis {axis}");
            }

            if (!status.IsEnabled)
            {
                _logger.LogWarning("Axis {Axis} is not enabled", axis);
                return MotionResult.Failure(MotionErrorCode.NotHomed,
                    $"Axis {axis} is not enabled");
            }

            if (status.HasAlarm)
            {
                _logger.LogWarning("Axis {Axis} has active alarm: {Message}", axis, status.AlarmMessage);
                return MotionResult.Failure(MotionErrorCode.AlarmActive,
                    $"Axis {axis} has alarm: {status.AlarmMessage}");
            }
        }

        _logger.LogInformation("Pre-condition checks passed");
        return MotionResult.Ok();
    }

    private async Task<MotionResult> HomeSingleAxisAsync(
        AxisId axis, HomingTrigger successTrigger, CancellationToken ct)
    {
        _logger.LogInformation("Starting homing for axis {Axis}", axis);

        // 安全校验
        var statuses = await GetAllAxisStatusesAsync();
        var safetyCheck = await _safety.ValidateHomingAsync(statuses);
        if (!safetyCheck.IsSafe)
        {
            _logger.LogWarning("Safety validation failed before homing {Axis}: {Reason}",
                axis, safetyCheck.ViolationReason);
            return MotionResult.Failure(MotionErrorCode.AlarmActive,
                safetyCheck.ViolationReason ?? "Safety violation");
        }

        // 带超时的回零
        using var timeoutCts = new CancellationTokenSource(_axisHomingTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var result = await _motion.HomeAsync(axis, linkedCts.Token);
            if (result.IsSuccess)
            {
                _fsm.Fire(successTrigger);
                _logger.LogInformation("Axis {Axis} homed successfully", axis);
            }
            else
            {
                _logger.LogError("Axis {Axis} homing failed: {Error}", axis, result.ErrorMessage);
            }

            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogError("Axis {Axis} homing timed out after {Timeout}s", axis, _axisHomingTimeout.TotalSeconds);
            return MotionResult.Failure(MotionErrorCode.Timeout,
                $"Axis {axis} homing timed out after {_axisHomingTimeout.TotalSeconds}s");
        }
    }

    private async Task<IReadOnlyDictionary<AxisId, AxisStatus>> GetAllAxisStatusesAsync()
    {
        var tasks = new[] { AxisId.X, AxisId.Y, AxisId.Z }
            .Select(async a => (axis: a, status: await _motion.GetAxisStatusAsync(a)));

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.axis, r => r.status);
    }

    private HomingResult BuildAxisFailure(MotionResult result, DateTime startTime)
    {
        var duration = DateTime.UtcNow - startTime;

        // 取消独立路由 → Cancelled 状态，不进入 Failed
        if (result.ErrorCode == MotionErrorCode.Cancelled)
        {
            _fsm.Fire(HomingTrigger.Cancel);
            return HomingResult.Cancel(duration);
        }

        var reason = result.ErrorCode switch
        {
            MotionErrorCode.Timeout => HomingFailureReason.AxisHomingTimeout,
            MotionErrorCode.AlarmActive => HomingFailureReason.AxisAlarmDuringHoming,
            _ => HomingFailureReason.AxisAlarmDuringHoming
        };

        _fsm.Fire(HomingTrigger.AxisFailed);
        return HomingResult.Failure(reason, result.ErrorMessage ?? "Axis homing failed", duration);
    }

    // ── 事件触发 ───────────────────────────────────────────

    private void OnStateChanged(HomingStateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }

    // ── 内部状态触发器枚举 ─────────────────────────────────

    private enum HomingTrigger
    {
        Start,
        PreCheckOk,
        PreCheckFailed,
        ZHomed,
        XHomed,
        YHomed,
        AxisFailed,
        Cancel,
        Reset
    }
}
