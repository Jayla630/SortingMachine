// =========================================================
// File: Domain/StateMachines/MotionSafetyValidator.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Claude Code
// =========================================================

using Microsoft.Extensions.Logging;
using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Domain.StateMachines;

/// <summary>
/// 运动安全校验实现 —— 四条核心规则：软限位、Z 轴安全高度、报警互锁、使能检查。
/// </summary>
public sealed class MotionSafetyValidator : ISafetyValidator
{
    private readonly IMotionController _motion;
    private readonly ILogger<MotionSafetyValidator> _logger;

    private const double SafeZHeight = 10.0;
    private const double MaxXYMoveWhenZLow = 50.0;
    private const double ProximityThreshold = 5.0;

    public MotionSafetyValidator(IMotionController motion, ILogger<MotionSafetyValidator> logger)
    {
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SafetyCheckResult> ValidateMoveAsync(AxisId axis, double targetPosition,
        IReadOnlyDictionary<AxisId, AxisStatus> currentStatuses)
    {
        // 1. 轴存在
        if (!currentStatuses.TryGetValue(axis, out var axisStatus))
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.ControllerNotReady,
                $"Axis {axis} status not available"));

        // 2. 使能检查
        if (!axisStatus.IsEnabled)
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.AxisNotEnabled,
                $"Axis {axis} is not enabled"));

        // 3. 报警互锁
        if (axisStatus.HasAlarm)
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.AxisHasAlarm,
                $"Axis {axis} has active alarm: {axisStatus.AlarmMessage}"));

        // 4. 软限位检查
        var config = GetAxisConfig(axis);
        if (targetPosition < config.NegativeLimit || targetPosition > config.PositiveLimit)
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.SoftLimitViolation,
                $"Target {targetPosition:F2}mm out of soft limit range [{config.NegativeLimit}, {config.PositiveLimit}] for axis {axis}"));

        // 5. Z 轴安全高度：非 Z 轴运动时，Z 必须在安全高度（≤10mm）或移动距离 ≤50mm
        if (axis != AxisId.Z && currentStatuses.TryGetValue(AxisId.Z, out var zStatus))
        {
            if (zStatus.Position > SafeZHeight)
            {
                var moveDistance = Math.Abs(targetPosition - axisStatus.Position);
                if (moveDistance > MaxXYMoveWhenZLow)
                    return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.ZAxisNotSafe,
                        $"Z axis at {zStatus.Position:F2}mm (> safe height {SafeZHeight}mm), " +
                        $"rejecting {axis} move of {moveDistance:F2}mm (> max {MaxXYMoveWhenZLow}mm)"));
            }
        }

        _logger.LogDebug("Move validation passed for axis {Axis} to target {Target:F2}", axis, targetPosition);
        return Task.FromResult(SafetyCheckResult.Safe());
    }

    public Task<SafetyCheckResult> ValidateHomingAsync(
        IReadOnlyDictionary<AxisId, AxisStatus> currentStatuses)
    {
        foreach (var axisId in new[] { AxisId.X, AxisId.Y, AxisId.Z })
        {
            if (!currentStatuses.TryGetValue(axisId, out var status))
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.ControllerNotReady,
                    $"Axis {axisId} status not available"));

            if (!status.IsEnabled)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.AxisNotEnabled,
                    $"Axis {axisId} is not enabled"));

            if (status.HasAlarm)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.AxisHasAlarm,
                    $"Axis {axisId} has active alarm: {status.AlarmMessage}"));
        }

        _logger.LogDebug("Homing validation passed");
        return Task.FromResult(SafetyCheckResult.Safe());
    }

    public Task<SafetyCheckResult> ValidateJogAsync(AxisId axis, double velocity,
        IReadOnlyDictionary<AxisId, AxisStatus> currentStatuses)
    {
        if (!currentStatuses.TryGetValue(axis, out var status))
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.ControllerNotReady,
                $"Axis {axis} status not available"));

        if (!status.IsEnabled)
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.AxisNotEnabled,
                $"Axis {axis} is not enabled"));

        if (status.HasAlarm)
            return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.AxisHasAlarm,
                $"Axis {axis} has active alarm"));

        var config = GetAxisConfig(axis);

        // 正方向 Jog：硬限位已触发 or 距离正限位 < 5mm → 拒绝
        if (velocity > 0)
        {
            if (status.PositiveLimitHit)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.SoftLimitViolation,
                    $"Cannot jog {axis} positive: already at positive limit"));

            if (status.Position >= config.PositiveLimit - ProximityThreshold)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.SoftLimitViolation,
                    $"Axis {axis} at {status.Position:F2}mm, within {ProximityThreshold}mm of positive limit {config.PositiveLimit}, rejecting positive jog"));
        }

        // 负方向 Jog：硬限位已触发 or 距离负限位 < 5mm → 拒绝
        if (velocity < 0)
        {
            if (status.NegativeLimitHit)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.SoftLimitViolation,
                    $"Cannot jog {axis} negative: already at negative limit"));

            if (status.Position <= config.NegativeLimit + ProximityThreshold)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.SoftLimitViolation,
                    $"Axis {axis} at {status.Position:F2}mm, within {ProximityThreshold}mm of negative limit {config.NegativeLimit}, rejecting negative jog"));
        }

        // Z 轴安全高度：非 Z 轴 jog 时 Z 必须在安全高度
        if (axis != AxisId.Z && currentStatuses.TryGetValue(AxisId.Z, out var zStatus))
        {
            if (zStatus.Position > SafeZHeight)
                return Task.FromResult(SafetyCheckResult.Unsafe(SafetyViolationType.ZAxisNotSafe,
                    $"Z axis at {zStatus.Position:F2}mm (> safe height {SafeZHeight}mm), cannot jog {axis}"));
        }

        _logger.LogDebug("Jog validation passed for axis {Axis}, velocity {Velocity:F2}", axis, velocity);
        return Task.FromResult(SafetyCheckResult.Safe());
    }

    private static AxisConfig GetAxisConfig(AxisId axis) => axis switch
    {
        AxisId.X => AxisConfig.DefaultX,
        AxisId.Y => AxisConfig.DefaultY,
        AxisId.Z => AxisConfig.DefaultZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };
}
