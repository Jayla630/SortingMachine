// =========================================================
// File: Domain/Recipe/CellGrade.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain.Recipe;

/// <summary>
/// 电芯等级 —— OCV/IR 检测后的分选等级。
/// </summary>
public enum CellGrade
{
    A,    // 优等品
    B,    // 良品
    C,    // 次品（降级使用）
    NG    // 不良品（报废）
}

/// <summary>
/// 等级判定结果 —— 包含判定依据，用于质量追溯。
/// </summary>
public record GradeDecision
{
    public CellGrade Grade { get; init; }
    public string Reason { get; init; } = string.Empty;
    /// <summary>触发该等级判定的关键指标（如 "OCV=3450mV"）</summary>
    public string? TriggeringMetric { get; init; }
}
