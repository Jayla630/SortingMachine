// =========================================================
// File: Infrastructure/Motion/IMotionController.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Infrastructure.Motion;

/// <summary>
/// 运动控制抽象接口 —— 完全厂商无关，不暴露任何驱动 SDK 类型。
/// 分选机全操作集：初始化、使能、回零、绝对/相对运动、点动、停止、急停、报警处理。
/// </summary>
public interface IMotionController
{
    // ── 事件 ────────────────────────────────────────────

    /// <summary>轴状态发生变化时触发。</summary>
    event EventHandler<AxisStatusChangedEventArgs>? AxisStatusChanged;

    /// <summary>驱动报警发生时触发。</summary>
    event EventHandler<MotionAlarmEventArgs>? AlarmOccurred;

    // ── 初始化 / 连接 ─────────────────────────────────

    /// <summary>初始化运动控制器（建立通信、配置各轴参数）。</summary>
    Task<MotionResult> InitializeAsync(CancellationToken ct = default);

    /// <summary>断开控制器连接，释放资源。</summary>
    Task<MotionResult> DisconnectAsync();

    // ── 轴使能控制 ─────────────────────────────────────

    /// <summary>伺服使能指定轴。</summary>
    Task<MotionResult> EnableAxisAsync(AxisId axis, CancellationToken ct = default);

    /// <summary>断开指定轴伺服使能。</summary>
    Task<MotionResult> DisableAxisAsync(AxisId axis);

    // ── 回零 ───────────────────────────────────────────

    /// <summary>指定轴执行回零动作。</summary>
    Task<MotionResult> HomeAsync(AxisId axis, CancellationToken ct = default);

    /// <summary>全轴回零 —— 按 Z→X→Y 顺序（先抬吸嘴避免碰撞）。</summary>
    Task<MotionResult> HomeAllAxesAsync(CancellationToken ct = default);

    // ── 运动控制 ───────────────────────────────────────

    /// <summary>绝对定位：移动到目标位置。</summary>
    Task<MotionResult> MoveAbsoluteAsync(AxisId axis, double position, double velocity, CancellationToken ct = default);

    /// <summary>相对定位：从当前位置移动指定距离。</summary>
    Task<MotionResult> MoveRelativeAsync(AxisId axis, double distance, double velocity, CancellationToken ct = default);

    /// <summary>点动启动（不等待完成，调用 StopAsync 停止）。</summary>
    Task<MotionResult> JogAsync(AxisId axis, double velocity);

    /// <summary>减速停止指定轴。</summary>
    Task<MotionResult> StopAsync(AxisId axis);

    /// <summary>紧急停止所有轴（立即切断，不减速）。</summary>
    Task<MotionResult> EmergencyStopAsync();

    // ── 状态查询 ───────────────────────────────────────

    /// <summary>获取指定轴的完整状态快照。</summary>
    Task<AxisStatus> GetAxisStatusAsync(AxisId axis);

    /// <summary>查询指定轴是否已完成回零。</summary>
    Task<bool> IsAxisHomedAsync(AxisId axis);

    /// <summary>全轴就绪检查：使能 + 回零 + 无报警。</summary>
    Task<bool> IsAllAxesReadyAsync();

    // ── 报警处理 ───────────────────────────────────────

    /// <summary>清除指定轴报警。</summary>
    Task<MotionResult> ClearAlarmAsync(AxisId axis);

    /// <summary>清除所有轴报警。</summary>
    Task<MotionResult> ClearAllAlarmsAsync();
}

#region DesignNotes
// [决策] 为什么用 MotionResult 而非 exception：
//   工控场景下驱动层错误（超时、限位、报警）是常态运行路径，
//   不是"异常"。返回 MotionResult 让调用方显式检查 IsSuccess，
//   避免 try/catch 淹没业务逻辑，也避免异常抛出的性能开销。

// [决策] 为什么 HomeAllAxesAsync 固定 Z→X→Y 顺序：
//   Z 轴是吸嘴升降轴，必须先抬升到安全高度再移动 XY，
//   否则可能发生吸嘴与料仓/治具的物理碰撞。X→Y 顺序可互换，
//   但 Z 必须先归零。

// [决策] 为什么 AxisId 用强类型枚举而非 int：
//   雷赛等驱动 SDK 使用 int 轴号传递，极易误写（如把 X 写成 3）。
//   在接口层封死为 AxisId 枚举，由驱动适配层完成枚举→int 映射，
//   杜绝 magic number 在整个上层业务代码中扩散。

// [决策] 为什么 JogAsync 和 StopAsync 不接受 CancellationToken：
//   点动是"启动后不等待完成"的操作，本身不阻塞；
//   停止是"终结"操作，不可取消。两者语义上不需要 CancellationToken。

// [决策] 为什么 DisconnectAsync 不接受 CancellationToken：
//   断开连接是资源释放操作，必须执行完成，不应被取消。
//   即使被取消也应该继续完成断开流程，因此不暴露 ct 参数。
#endregion
