// =========================================================
// File: Domain/StateMachines/IHomingStateMachine.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain.StateMachines;

/// <summary>
/// 回零状态机接口 —— 封装三轴顺序回零流程（Z→X→Y）。
/// </summary>
public interface IHomingStateMachine
{
    /// <summary>当前回零状态</summary>
    HomingState CurrentState { get; }

    /// <summary>
    /// 执行完整三轴回零序列（Z → X → Y）。
    /// 已在 Completed 状态时直接返回 Success，实现幂等。
    /// </summary>
    Task<HomingResult> ExecuteAsync(CancellationToken ct = default);

    /// <summary>取消正在进行的回零</summary>
    Task CancelAsync();

    /// <summary>重置到 Idle（用于异常恢复后重试）</summary>
    void Reset();

    /// <summary>状态变化事件，用于 UI 绑定和日志记录</summary>
    event EventHandler<HomingStateChangedEventArgs>? StateChanged;
}

#region DesignNotes
// [决策] 为什么用 Stateless 而不是手写 switch-case：
//   Stateless 以声明式配置定义状态转换，天然防止非法状态跃迁；
//   手写 switch-case 在状态增多时爆炸式增长，且无法在编译期发现
//   遗漏的转换路径。对于工控状态机这种正确性优先的场景，
//   库级保证远比手工维护的代码可靠。

// [决策] 为什么 Z 轴必须先回零：
//   Z 轴是吸嘴升降轴，回零过程会将吸嘴抬升到机械原点（最高点）。
//   如果先移动 X/Y 再回零 Z，吸嘴可能在下降位移动，
//   与料仓、治具发生物理碰撞。Z 先回零 = 先确保"缩回"，安全第一。

// [决策] 为什么把 SafetyValidator 独立出来而不是内联在状态机里：
//   安全校验是一组通用规则（软限位、碰撞区域、互锁），不仅用于回零，
//   也用于绝对运动、Jog、后续的自动分选流程。独立接口让所有调用方
//   共享同一套安全逻辑，避免规则散落在不同类中导致不一致。
//   类似电梯的门联锁独立于楼层调度逻辑存在。
#endregion
