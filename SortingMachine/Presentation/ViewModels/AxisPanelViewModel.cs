// =========================================================
// File: Presentation/ViewModels/AxisPanelViewModel.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Gemini CLI
// =========================================================
using Prism.Mvvm;
using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Presentation.ViewModels;

public class AxisPanelViewModel : BindableBase
{
    private double _position;
    private double _velocity;
    private bool _isEnabled;
    private bool _isHomed;
    private bool _isMoving;
    private bool _hasAlarm;
    private string? _alarmMessage;
    private bool _positiveLimitHit;
    private bool _negativeLimitHit;
    private double _jogVelocity = 10.0;
    private double _targetPosition;
    private double _moveVelocity = 50.0;

    public AxisPanelViewModel(AxisId axisId, string axisName)
    {
        AxisId = axisId;
        AxisName = axisName;
    }

    public AxisId AxisId { get; }
    public string AxisName { get; }

    public double Position
    {
        get => _position;
        set => SetProperty(ref _position, value);
    }

    public double Velocity
    {
        get => _velocity;
        set => SetProperty(ref _velocity, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsHomed
    {
        get => _isHomed;
        set => SetProperty(ref _isHomed, value);
    }

    public bool IsMoving
    {
        get => _isMoving;
        set => SetProperty(ref _isMoving, value);
    }

    public bool HasAlarm
    {
        get => _hasAlarm;
        set => SetProperty(ref _hasAlarm, value);
    }

    public string? AlarmMessage
    {
        get => _alarmMessage;
        set => SetProperty(ref _alarmMessage, value);
    }

    public bool PositiveLimitHit
    {
        get => _positiveLimitHit;
        set => SetProperty(ref _positiveLimitHit, value);
    }

    public bool NegativeLimitHit
    {
        get => _negativeLimitHit;
        set => SetProperty(ref _negativeLimitHit, value);
    }

    public double JogVelocity
    {
        get => _jogVelocity;
        set => SetProperty(ref _jogVelocity, value);
    }

    public double TargetPosition
    {
        get => _targetPosition;
        set => SetProperty(ref _targetPosition, value);
    }

    public double MoveVelocity
    {
        get => _moveVelocity;
        set => SetProperty(ref _moveVelocity, value);
    }

    public void UpdateFromStatus(AxisStatus status)
    {
        Position = status.Position;
        Velocity = status.Velocity;
        IsEnabled = status.IsEnabled;
        IsHomed = status.IsHomed;
        IsMoving = status.IsMoving;
        HasAlarm = status.HasAlarm;
        AlarmMessage = status.AlarmMessage;
        PositiveLimitHit = status.PositiveLimitHit;
        NegativeLimitHit = status.NegativeLimitHit;
    }
}
