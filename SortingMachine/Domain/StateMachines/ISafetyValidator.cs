// =========================================================
// File: Domain/StateMachines/ISafetyValidator.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Claude Code
// =========================================================

using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Domain.StateMachines;

/// <summary>
/// 运动安全校验接口 —— 所有运动命令在执行前必须通过此校验。
/// </summary>
public interface ISafetyValidator
{
    /// <summary>
    /// 校验绝对运动是否安全：
    /// - 目标位置在软限位范围内
    /// - 不进入碰撞区域
    /// - Z 轴未在危险高度时 X/Y 不允许大范围移动
    /// </summary>
    Task<SafetyCheckResult> ValidateMoveAsync(AxisId axis, double targetPosition,
        IReadOnlyDictionary<AxisId, AxisStatus> currentStatuses);

    /// <summary>校验回零前置条件</summary>
    Task<SafetyCheckResult> ValidateHomingAsync(
        IReadOnlyDictionary<AxisId, AxisStatus> currentStatuses);

    /// <summary>校验 Jog 方向是否安全（不会立即越限）</summary>
    Task<SafetyCheckResult> ValidateJogAsync(AxisId axis, double velocity,
        IReadOnlyDictionary<AxisId, AxisStatus> currentStatuses);
}

/// <summary>
/// 安全违规类型。
/// </summary>
public enum SafetyViolationType
{
    None,
    SoftLimitViolation,
    CollisionZone,
    ZAxisNotSafe,
    AxisNotEnabled,
    AxisHasAlarm,
    ControllerNotReady
}

/// <summary>
/// 安全校验结果 —— 替代异常驱动，所有校验方法返回此类型。
/// </summary>
public sealed record SafetyCheckResult
{
    public bool IsSafe { get; init; }
    public SafetyViolationType ViolationType { get; init; }
    public string? ViolationReason { get; init; }

    public static SafetyCheckResult Safe()
        => new() { IsSafe = true, ViolationType = SafetyViolationType.None };

    public static SafetyCheckResult Unsafe(SafetyViolationType type, string reason)
        => new() { IsSafe = false, ViolationType = type, ViolationReason = reason };
}
