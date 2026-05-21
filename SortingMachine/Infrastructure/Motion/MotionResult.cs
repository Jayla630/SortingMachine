// =========================================================
// File: Infrastructure/Motion/MotionResult.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Infrastructure.Motion;

/// <summary>
/// 运动控制错误码 —— 驱动层错误是工控场景的"正常"业务路径。
/// </summary>
public enum MotionErrorCode
{
    None = 0,
    Timeout,
    LimitHit,
    AlarmActive,
    NotHomed,
    HardwareError,
    Cancelled
}

/// <summary>
/// 运动控制统一返回值 —— 替代异常驱动的流程控制。
/// 所有 IMotionController 方法返回 Task&lt;MotionResult&gt;，
/// 调用方检查 IsSuccess 决定后续路径，不依赖 try/catch。
/// </summary>
public sealed record MotionResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public MotionErrorCode ErrorCode { get; init; }

    public static MotionResult Ok() => new()
    {
        IsSuccess = true,
        ErrorCode = MotionErrorCode.None
    };

    public static MotionResult Failure(MotionErrorCode code, string message) => new()
    {
        IsSuccess = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
