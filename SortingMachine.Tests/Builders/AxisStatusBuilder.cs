// =========================================================
// File: SortingMachine.Tests/Builders/AxisStatusBuilder.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Codex
// =========================================================

using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Tests.Builders;

public sealed class AxisStatusBuilder
{
    private AxisId _axisId = AxisId.X;
    private double _position;
    private double _velocity;
    private bool _isEnabled;
    private bool _isHomed;
    private bool _isMoving;
    private bool _positiveLimitHit;
    private bool _negativeLimitHit;
    private bool _hasAlarm;
    private string? _alarmMessage;
    private DateTime _timestamp = DateTime.UtcNow;

    public AxisStatusBuilder ForAxis(AxisId axisId)
    {
        _axisId = axisId;
        return this;
    }

    public AxisStatusBuilder AtPosition(double position)
    {
        _position = position;
        return this;
    }

    public AxisStatusBuilder WithVelocity(double velocity)
    {
        _velocity = velocity;
        return this;
    }

    public AxisStatusBuilder Enabled()
    {
        _isEnabled = true;
        return this;
    }

    public AxisStatusBuilder Disabled()
    {
        _isEnabled = false;
        return this;
    }

    public AxisStatusBuilder Homed()
    {
        _isHomed = true;
        return this;
    }

    public AxisStatusBuilder NotHomed()
    {
        _isHomed = false;
        return this;
    }

    public AxisStatusBuilder Moving()
    {
        _isMoving = true;
        return this;
    }

    public AxisStatusBuilder Stopped()
    {
        _isMoving = false;
        _velocity = 0;
        return this;
    }

    public AxisStatusBuilder WithPositiveLimitHit()
    {
        _positiveLimitHit = true;
        return this;
    }

    public AxisStatusBuilder WithNegativeLimitHit()
    {
        _negativeLimitHit = true;
        return this;
    }

    public AxisStatusBuilder WithAlarm(string message = "Simulated alarm")
    {
        _hasAlarm = true;
        _alarmMessage = message;
        return this;
    }

    public AxisStatusBuilder WithoutAlarm()
    {
        _hasAlarm = false;
        _alarmMessage = null;
        return this;
    }

    public AxisStatusBuilder WithTimestamp(DateTime timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    public AxisStatus Build()
    {
        return new AxisStatus
        {
            AxisId = _axisId,
            Position = _position,
            Velocity = _velocity,
            IsEnabled = _isEnabled,
            IsHomed = _isHomed,
            IsMoving = _isMoving,
            PositiveLimitHit = _positiveLimitHit,
            NegativeLimitHit = _negativeLimitHit,
            HasAlarm = _hasAlarm,
            AlarmMessage = _alarmMessage,
            Timestamp = _timestamp
        };
    }
}

