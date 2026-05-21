// =========================================================
// File: Infrastructure/Motion/MockMotionController.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Claude Code
// =========================================================

using System.Collections.Concurrent;

namespace SortingMachine.Infrastructure.Motion;

/// <summary>
/// 纯软件模拟运动控制器 —— 不依赖任何硬件，使全链路业务逻辑可在开发机完整运行。
///
/// 模拟行为概要：
/// - 状态存储：ConcurrentDictionary&lt;AxisId, AxisStatus&gt; 维护各轴状态
/// - 运动耗时：MoveAbsoluteAsync 根据距离/速度计算 delay = |target-current| / v * 1000 (ms)
/// - 限位检查：移动前检查目标位置是否在配置的软限位范围内，越限返回 Failure
/// - 急停：EmergencyStopAsync 将所有运动中的轴立即置为 Faulted（HasAlarm=true, IsMoving=false）
/// - 回零：HomeAsync 延迟 2 秒后将轴位置归零，状态改为 Homed
/// - 报警注入：SimulateAlarm 方法用于测试异常恢复路径（非接口方法，Mock 专属）
/// </summary>
public sealed class MockMotionController : IMotionController
{
    private readonly ConcurrentDictionary<AxisId, AxisStatus> _axes = new();
    private readonly Dictionary<AxisId, AxisConfig> _configs;
    private readonly Dictionary<AxisId, CancellationTokenSource?> _activeMovementCts = new();
    private bool _isInitialized;

    public event EventHandler<AxisStatusChangedEventArgs>? AxisStatusChanged;
    public event EventHandler<MotionAlarmEventArgs>? AlarmOccurred;

    /// <summary>
    /// 使用默认轴配置构造 Mock。
    /// </summary>
    public MockMotionController()
        : this(new Dictionary<AxisId, AxisConfig>
        {
            [AxisId.X] = AxisConfig.DefaultX,
            [AxisId.Y] = AxisConfig.DefaultY,
            [AxisId.Z] = AxisConfig.DefaultZ
        })
    {
    }

    /// <summary>
    /// 使用自定义轴配置构造 Mock（允许调用方覆盖软限位等参数）。
    /// </summary>
    public MockMotionController(Dictionary<AxisId, AxisConfig> configs)
    {
        _configs = configs;

        foreach (var (id, config) in configs)
        {
            _axes[id] = new AxisStatus
            {
                AxisId = id,
                Position = config.OriginOffset,
                Velocity = 0,
                IsEnabled = false,
                IsHomed = false,
                IsMoving = false,
                PositiveLimitHit = false,
                NegativeLimitHit = false,
                HasAlarm = false,
                AlarmMessage = null,
                Timestamp = DateTime.UtcNow
            };
            _activeMovementCts[id] = null;
        }
    }

    // ── 初始化 / 连接 ─────────────────────────────────

    /// <summary>模拟初始化：将所有轴状态置为未使能、未回零。</summary>
    public Task<MotionResult> InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized)
            return Task.FromResult(MotionResult.Ok());

        foreach (var id in _axes.Keys.ToList())
        {
            var old = _axes[id];
            _axes[id] = old with
            {
                IsEnabled = false,
                IsHomed = false,
                IsMoving = false,
                HasAlarm = false,
                AlarmMessage = null,
                PositiveLimitHit = false,
                NegativeLimitHit = false,
                Timestamp = DateTime.UtcNow
            };
            RaiseStatusChanged(old, _axes[id]);
        }

        _isInitialized = true;
        return Task.FromResult(MotionResult.Ok());
    }

    /// <summary>模拟断开：清空所有轴状态。</summary>
    public Task<MotionResult> DisconnectAsync()
    {
        CancelAllMovements();

        foreach (var id in _axes.Keys.ToList())
        {
            var old = _axes[id];
            _axes[id] = old with
            {
                IsEnabled = false,
                IsHomed = false,
                IsMoving = false,
                Velocity = 0,
                HasAlarm = false,
                AlarmMessage = null,
                PositiveLimitHit = false,
                NegativeLimitHit = false,
                Timestamp = DateTime.UtcNow
            };
        }

        _isInitialized = false;
        return Task.FromResult(MotionResult.Ok());
    }

    // ── 轴使能控制 ─────────────────────────────────────

    /// <summary>模拟伺服使能：将轴置为 IsEnabled=true。</summary>
    public Task<MotionResult> EnableAxisAsync(AxisId axis, CancellationToken ct = default)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found"));

        if (current.HasAlarm)
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.AlarmActive, $"Axis {axis} has active alarm: {current.AlarmMessage}"));

        var updated = current with { IsEnabled = true, Timestamp = DateTime.UtcNow };
        _axes[axis] = updated;
        RaiseStatusChanged(current, updated);
        return Task.FromResult(MotionResult.Ok());
    }

    /// <summary>模拟断开使能：将轴置为 IsEnabled=false。</summary>
    public Task<MotionResult> DisableAxisAsync(AxisId axis)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found"));

        CancelAxisMovement(axis);

        var updated = current with
        {
            IsEnabled = false,
            IsMoving = false,
            Velocity = 0,
            Timestamp = DateTime.UtcNow
        };
        _axes[axis] = updated;
        RaiseStatusChanged(current, updated);
        return Task.FromResult(MotionResult.Ok());
    }

    // ── 回零 ───────────────────────────────────────────

    /// <summary>模拟回零：延迟 2 秒后将轴位置归零，状态变为 Homed。</summary>
    public async Task<MotionResult> HomeAsync(AxisId axis, CancellationToken ct = default)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found");

        if (!current.IsEnabled)
            return MotionResult.Failure(MotionErrorCode.NotHomed, $"Axis {axis} is not enabled");

        if (current.HasAlarm)
            return MotionResult.Failure(MotionErrorCode.AlarmActive, $"Axis {axis} has active alarm: {current.AlarmMessage}");

        // 创建轴级 CTS，允许 SimulateAlarm 中断回零
        var axisCts = new CancellationTokenSource();
        _activeMovementCts[axis] = axisCts;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, axisCts.Token);

        try
        {
            // 模拟回零耗时 2 秒
            var moving = current with { IsMoving = true, Velocity = 10, Timestamp = DateTime.UtcNow };
            _axes[axis] = moving;
            RaiseStatusChanged(current, moving);

            await Task.Delay(2000, linkedCts.Token);

            // 延迟完成后检查是否有报警（SimulateAlarm 可能在等待期间被调用）
            var statusAfterDelay = _axes[axis];
            if (statusAfterDelay.HasAlarm)
            {
                var stopped = statusAfterDelay with { IsMoving = false, Velocity = 0, Timestamp = DateTime.UtcNow };
                _axes[axis] = stopped;
                RaiseStatusChanged(statusAfterDelay, stopped);
                return MotionResult.Failure(MotionErrorCode.AlarmActive,
                    statusAfterDelay.AlarmMessage ?? "Alarm interrupted homing");
            }

            var config = _configs[axis];
            var homed = statusAfterDelay with
            {
                Position = config.OriginOffset,
                Velocity = 0,
                IsMoving = false,
                IsHomed = true,
                PositiveLimitHit = false,
                NegativeLimitHit = false,
                Timestamp = DateTime.UtcNow
            };
            _axes[axis] = homed;
            RaiseStatusChanged(statusAfterDelay, homed);

            return MotionResult.Ok();
        }
        catch (OperationCanceledException)
        {
            // 区分：报警触发取消 or 外部取消
            var alarmed = _axes[axis];
            if (alarmed.HasAlarm)
            {
                var stopped = alarmed with { IsMoving = false, Velocity = 0, Timestamp = DateTime.UtcNow };
                _axes[axis] = stopped;
                RaiseStatusChanged(alarmed, stopped);
                return MotionResult.Failure(MotionErrorCode.AlarmActive,
                    alarmed.AlarmMessage ?? "Alarm interrupted homing");
            }

            var cancelled = current with { IsMoving = false, Velocity = 0, Timestamp = DateTime.UtcNow };
            _axes[axis] = cancelled;
            RaiseStatusChanged(current, cancelled);
            return MotionResult.Failure(MotionErrorCode.Cancelled, $"HomeAsync for {axis} was cancelled");
        }
        finally
        {
            _activeMovementCts[axis] = null;
            axisCts.Dispose();
        }
    }

    /// <summary>全轴回零：复用 HomeAsync，按 Z→X→Y 顺序执行。</summary>
    public async Task<MotionResult> HomeAllAxesAsync(CancellationToken ct = default)
    {
        // Z 先归零（抬升吸嘴避免碰撞），再 X、Y
        var order = new[] { AxisId.Z, AxisId.X, AxisId.Y };
        foreach (var axis in order)
        {
            var result = await HomeAsync(axis, ct);
            if (!result.IsSuccess)
                return result;
        }
        return MotionResult.Ok();
    }

    // ── 运动控制 ───────────────────────────────────────

    /// <summary>模拟绝对定位：根据距离/速度计算耗时，Task.Delay 模拟运动过程。</summary>
    public async Task<MotionResult> MoveAbsoluteAsync(AxisId axis, double position, double velocity, CancellationToken ct = default)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found");

        if (!current.IsEnabled)
            return MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} is not enabled");

        if (!current.IsHomed)
            return MotionResult.Failure(MotionErrorCode.NotHomed, $"Axis {axis} is not homed");

        if (current.HasAlarm)
            return MotionResult.Failure(MotionErrorCode.AlarmActive, $"Axis {axis} has active alarm: {current.AlarmMessage}");

        var config = _configs[axis];

        // Soft limit check
        if (position < config.NegativeLimit)
        {
            var limitHit = current with
            {
                Position = config.NegativeLimit,
                NegativeLimitHit = true,
                IsMoving = false,
                Velocity = 0,
                Timestamp = DateTime.UtcNow
            };
            _axes[axis] = limitHit;
            RaiseStatusChanged(current, limitHit);
            return MotionResult.Failure(MotionErrorCode.LimitHit,
                $"Target position {position} exceeds negative limit {config.NegativeLimit} for axis {axis}");
        }

        if (position > config.PositiveLimit)
        {
            var limitHit = current with
            {
                Position = config.PositiveLimit,
                PositiveLimitHit = true,
                IsMoving = false,
                Velocity = 0,
                Timestamp = DateTime.UtcNow
            };
            _axes[axis] = limitHit;
            RaiseStatusChanged(current, limitHit);
            return MotionResult.Failure(MotionErrorCode.LimitHit,
                $"Target position {position} exceeds positive limit {config.PositiveLimit} for axis {axis}");
        }

        // 计算运动耗时 (ms)
        var distance = Math.Abs(position - current.Position);
        var delayMs = (int)(distance / velocity * 1000);
        delayMs = Math.Max(delayMs, 1); // 至少 1ms

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeMovementCts[axis] = cts;

        try
        {
            var moving = current with { IsMoving = true, Velocity = velocity, Timestamp = DateTime.UtcNow };
            _axes[axis] = moving;
            RaiseStatusChanged(current, moving);

            await Task.Delay(delayMs, cts.Token);

            var arrived = moving with
            {
                Position = position,
                Velocity = 0,
                IsMoving = false,
                Timestamp = DateTime.UtcNow
            };
            _axes[axis] = arrived;
            _activeMovementCts[axis] = null;
            RaiseStatusChanged(moving, arrived);

            return MotionResult.Ok();
        }
        catch (OperationCanceledException)
        {
            var cancelled = _axes[axis] with { IsMoving = false, Velocity = 0, Timestamp = DateTime.UtcNow };
            _axes[axis] = cancelled;
            _activeMovementCts[axis] = null;
            RaiseStatusChanged(current, cancelled);
            return MotionResult.Failure(MotionErrorCode.Cancelled, $"MoveAbsoluteAsync for {axis} was cancelled");
        }
    }

    /// <summary>模拟相对定位：计算目标位置后委托给 MoveAbsoluteAsync。</summary>
    public async Task<MotionResult> MoveRelativeAsync(AxisId axis, double distance, double velocity, CancellationToken ct = default)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found");

        return await MoveAbsoluteAsync(axis, current.Position + distance, velocity, ct);
    }

    /// <summary>模拟点动：设定速度并标记运动中，不等待完成。</summary>
    public Task<MotionResult> JogAsync(AxisId axis, double velocity)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found"));

        if (!current.IsEnabled)
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} is not enabled"));

        if (!current.IsHomed)
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.NotHomed, $"Axis {axis} is not homed"));

        if (current.HasAlarm)
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.AlarmActive, $"Axis {axis} has active alarm"));

        var updated = current with { IsMoving = true, Velocity = velocity, Timestamp = DateTime.UtcNow };
        _axes[axis] = updated;
        RaiseStatusChanged(current, updated);
        return Task.FromResult(MotionResult.Ok());
    }

    /// <summary>模拟减速停止：将运动中的轴停止。</summary>
    public Task<MotionResult> StopAsync(AxisId axis)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found"));

        CancelAxisMovement(axis);

        var updated = current with { IsMoving = false, Velocity = 0, Timestamp = DateTime.UtcNow };
        _axes[axis] = updated;
        RaiseStatusChanged(current, updated);
        return Task.FromResult(MotionResult.Ok());
    }

    /// <summary>模拟急停：所有轴立即停止并进入 Faulted（HasAlarm）状态。</summary>
    public Task<MotionResult> EmergencyStopAsync()
    {
        CancelAllMovements();

        foreach (var id in _axes.Keys.ToList())
        {
            var old = _axes[id];
            var faulted = old with
            {
                IsMoving = false,
                Velocity = 0,
                HasAlarm = true,
                AlarmMessage = "Emergency stop activated",
                Timestamp = DateTime.UtcNow
            };
            _axes[id] = faulted;
            RaiseStatusChanged(old, faulted);
            RaiseAlarm(id, "Emergency stop activated");
        }

        return Task.FromResult(MotionResult.Ok());
    }

    // ── 状态查询 ───────────────────────────────────────

    /// <summary>获取指定轴的完整状态快照。</summary>
    public Task<AxisStatus> GetAxisStatusAsync(AxisId axis)
    {
        if (_axes.TryGetValue(axis, out var status))
            return Task.FromResult(status);
        return Task.FromResult(new AxisStatus
        {
            AxisId = axis,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>查询指定轴是否已完成回零。</summary>
    public Task<bool> IsAxisHomedAsync(AxisId axis)
    {
        if (_axes.TryGetValue(axis, out var status))
            return Task.FromResult(status.IsHomed);
        return Task.FromResult(false);
    }

    /// <summary>全轴就绪检查：使能 + 回零 + 无报警。</summary>
    public Task<bool> IsAllAxesReadyAsync()
    {
        var ready = _axes.Values.All(s => s.IsEnabled && s.IsHomed && !s.HasAlarm);
        return Task.FromResult(ready);
    }

    // ── 报警处理 ───────────────────────────────────────

    /// <summary>清除指定轴报警。</summary>
    public Task<MotionResult> ClearAlarmAsync(AxisId axis)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return Task.FromResult(MotionResult.Failure(MotionErrorCode.HardwareError, $"Axis {axis} not found"));

        var updated = current with { HasAlarm = false, AlarmMessage = null, Timestamp = DateTime.UtcNow };
        _axes[axis] = updated;
        RaiseStatusChanged(current, updated);
        return Task.FromResult(MotionResult.Ok());
    }

    /// <summary>清除所有轴报警。</summary>
    public Task<MotionResult> ClearAllAlarmsAsync()
    {
        foreach (var id in _axes.Keys.ToList())
        {
            var old = _axes[id];
            if (!old.HasAlarm) continue;
            var updated = old with { HasAlarm = false, AlarmMessage = null, Timestamp = DateTime.UtcNow };
            _axes[id] = updated;
            RaiseStatusChanged(old, updated);
        }
        return Task.FromResult(MotionResult.Ok());
    }

    // ── Mock 专属方法 ──────────────────────────────────

    /// <summary>
    /// 注入模拟报警 —— 测试异常恢复路径专用（非接口方法，Mock 专属）。
    /// </summary>
    public void SimulateAlarm(AxisId axis, string reason)
    {
        if (!_axes.TryGetValue(axis, out var current))
            return;

        CancelAxisMovement(axis);

        var alarmed = current with
        {
            IsMoving = false,
            Velocity = 0,
            HasAlarm = true,
            AlarmMessage = reason,
            Timestamp = DateTime.UtcNow
        };
        _axes[axis] = alarmed;
        RaiseStatusChanged(current, alarmed);
        RaiseAlarm(axis, reason);
    }

    // ── 内部辅助 ───────────────────────────────────────

    private void CancelAxisMovement(AxisId axis)
    {
        if (_activeMovementCts.TryGetValue(axis, out var cts) && cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            _activeMovementCts[axis] = null;
        }
    }

    private void CancelAllMovements()
    {
        foreach (var axis in _activeMovementCts.Keys.ToList())
        {
            CancelAxisMovement(axis);
        }
    }

    private void RaiseStatusChanged(AxisStatus previous, AxisStatus current)
    {
        AxisStatusChanged?.Invoke(this, new AxisStatusChangedEventArgs(previous, current));
    }

    private void RaiseAlarm(AxisId axis, string message)
    {
        AlarmOccurred?.Invoke(this, new MotionAlarmEventArgs(axis, message));
    }
}
