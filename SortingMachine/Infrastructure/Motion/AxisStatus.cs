// =========================================================
// File: Infrastructure/Motion/AxisStatus.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Infrastructure.Motion;

/// <summary>
/// 轴状态快照 —— 单次采样的全量轴信息。
/// </summary>
public sealed record AxisStatus
{
    public required AxisId AxisId { get; init; }
    public double Position { get; init; }
    public double Velocity { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsHomed { get; init; }
    public bool IsMoving { get; init; }
    public bool PositiveLimitHit { get; init; }
    public bool NegativeLimitHit { get; init; }
    public bool HasAlarm { get; init; }
    public string? AlarmMessage { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// 轴状态变更事件参数。
/// </summary>
public sealed class AxisStatusChangedEventArgs : EventArgs
{
    public AxisStatus PreviousStatus { get; }
    public AxisStatus CurrentStatus { get; }

    public AxisStatusChangedEventArgs(AxisStatus previous, AxisStatus current)
    {
        PreviousStatus = previous;
        CurrentStatus = current;
    }
}

/// <summary>
/// 报警事件参数。
/// </summary>
public sealed class MotionAlarmEventArgs : EventArgs
{
    public AxisId AxisId { get; }
    public string AlarmMessage { get; }
    public DateTime Timestamp { get; }

    public MotionAlarmEventArgs(AxisId axisId, string alarmMessage)
    {
        AxisId = axisId;
        AlarmMessage = alarmMessage;
        Timestamp = DateTime.UtcNow;
    }
}
