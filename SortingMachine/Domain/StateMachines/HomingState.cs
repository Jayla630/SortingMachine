// =========================================================
// File: Domain/StateMachines/HomingState.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain.StateMachines;

/// <summary>
/// 回零状态枚举 —— 从 Idle 到 Completed/Failed/Cancelled 的完整生命周期。
/// </summary>
public enum HomingState
{
    Idle,
    CheckingPreConditions,
    HomingZ,
    HomingX,
    HomingY,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// 回零失败原因枚举。
/// </summary>
public enum HomingFailureReason
{
    None,
    PreConditionFailed,
    AxisHomingTimeout,
    AxisAlarmDuringHoming,
    SafetyViolation,
    Cancelled
}

/// <summary>
/// 回零操作统一结果。
/// </summary>
public sealed record HomingResult
{
    public bool IsSuccess { get; init; }
    public HomingState FinalState { get; init; }
    public HomingFailureReason FailureReason { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    public static HomingResult Success(TimeSpan duration)
        => new() { IsSuccess = true, FinalState = HomingState.Completed, Duration = duration };

    public static HomingResult Failure(HomingFailureReason reason, string message, TimeSpan duration)
        => new()
        {
            IsSuccess = false,
            FinalState = HomingState.Failed,
            FailureReason = reason,
            ErrorMessage = message,
            Duration = duration
        };

    public static HomingResult Cancel(TimeSpan duration)
        => new()
        {
            IsSuccess = false,
            FinalState = HomingState.Cancelled,
            FailureReason = HomingFailureReason.Cancelled,
            Duration = duration
        };
}

/// <summary>
/// 回零状态变更事件参数。
/// </summary>
public sealed record HomingStateChangedEventArgs(
    HomingState PreviousState,
    HomingState CurrentState,
    string? Message = null
);
