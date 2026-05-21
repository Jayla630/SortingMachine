// =========================================================
// File: Domain/ISortingService.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

using SortingMachine.Domain.Recipe;

namespace SortingMachine.Domain;

/// <summary>
/// 分选服务接口 —— 编排单颗电芯从"检测数据"到"入仓计数"的完整流程。
/// </summary>
public interface ISortingService
{
    /// <summary>当前加载的配方（null = 未加载）</summary>
    SortingRecipe? ActiveRecipe { get; }

    /// <summary>加载配方（替换当前配方，运行中不允许切换）</summary>
    void LoadRecipe(SortingRecipe recipe);

    /// <summary>卸载配方</summary>
    void UnloadRecipe();

    /// <summary>
    /// 执行完整分选动作：判级 → 选仓 → 运动 → 放料 → 计数
    /// </summary>
    Task<SortingResult> SortCellAsync(CellMeasurement measurement,
        CancellationToken ct = default);

    /// <summary>
    /// 检查分选服务是否就绪：
    /// 配方已加载 + 三轴已回零 + 无报警
    /// </summary>
    Task<bool> IsReadyAsync();

    /// <summary>重置所有料仓计数（换产时使用）</summary>
    void ResetBinCounts();

    /// <summary>分选完成事件（成功或失败均触发）</summary>
    event EventHandler<SortingCompletedEventArgs> SortingCompleted;

    /// <summary>料仓满料事件</summary>
    event EventHandler<BinFullEventArgs> BinFull;
}

#region DesignNotes
// [决策] 为什么 Z 轴先抬起再 XY 移动（防撞）：
//   分选机械手的吸嘴在放料后可能处于低位。如果先移动 XY 再抬 Z，
//   吸嘴会横扫料仓阵列，撞坏吸嘴和电芯。必须先抬 Z 到安全高度，
//   再执行水平运动，最后下降放料。这是所有贴片机/点胶机的标准安全流程。

// [决策] 为什么用 SemaphoreSlim 防并发（产线节拍保障）：
//   OCV/IR 检测工位以固定节拍（~3s/颗）生产电芯。如果前一颗还在
//   分选运动中就接受下一颗的指令，两轴运动会互相干扰，导致位置超调
//   甚至碰撞。SemaphoreSlim(1,1) 保证单颗串行处理，匹配物理机械手的
//   独占性。如需提升吞吐，应增加机械手数量而非并发调用同一机械手。

// [决策] 为什么判级方法放在 GradingRules 而不是 SortingService（单一职责）：
//   GradingRules 是纯数据驱动的判定逻辑，无外部依赖，易于单元测试。
//   SortingService 是编排层，负责：判级（委托给 GradingRules）、选仓、
//   运动控制（委托给 IMotionController）、安全校验（委托给 ISafetyValidator）。
//   每个环节各司其职，修改阈值不会触碰运动代码，更换运动策略不影响判定逻辑。
#endregion
