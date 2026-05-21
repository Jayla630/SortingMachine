// =========================================================
// File: Domain/Recipe/GradingRules.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain.Recipe;

/// <summary>
/// OCV/IR 阈值分级规则（所有阈值含边界值）。
/// 判定优先级：NG → C → B → A（从严到宽，先排除不良品）。
/// </summary>
public record GradingRules
{
    // ── 总体合格范围（超出直接 NG） ──
    public double OcvMin { get; init; } = 3200;   // mV
    public double OcvMax { get; init; } = 4250;   // mV
    public double IrMin { get; init; } = 0;        // mΩ
    public double IrMax { get; init; } = 50;       // mΩ

    // ── A 级阈值 ──
    public double OcvMin_A { get; init; } = 3600;
    public double OcvMax_A { get; init; } = 4200;
    public double IrMax_A { get; init; } = 20;

    // ── B 级阈值 ──
    public double OcvMin_B { get; init; } = 3400;
    public double OcvMax_B { get; init; } = 4250;
    public double IrMax_B { get; init; } = 35;

    // C 级：其余合格范围（OcvMin~OcvMax 内，但不满足 A/B 条件）
    // NG：OCV 或 IR 超出总体范围

    /// <summary>核心判级方法 —— 按 NG→A→B→C 优先级判定。</summary>
    public GradeDecision DetermineGrade(double ocvMv, double irMohm)
    {
        // NG 判定（优先）
        if (ocvMv < OcvMin || ocvMv > OcvMax)
            return new GradeDecision
            {
                Grade = CellGrade.NG,
                Reason = "OCV 超出合格范围",
                TriggeringMetric = $"OCV={ocvMv}mV"
            };
        if (irMohm < IrMin || irMohm > IrMax)
            return new GradeDecision
            {
                Grade = CellGrade.NG,
                Reason = "IR 超出合格范围",
                TriggeringMetric = $"IR={irMohm}mΩ"
            };

        // A 级判定
        if (ocvMv >= OcvMin_A && ocvMv <= OcvMax_A && irMohm <= IrMax_A)
            return new GradeDecision { Grade = CellGrade.A, Reason = "A级品" };

        // B 级判定
        if (ocvMv >= OcvMin_B && ocvMv <= OcvMax_B && irMohm <= IrMax_B)
            return new GradeDecision { Grade = CellGrade.B, Reason = "B级品" };

        // C 级（兜底）
        return new GradeDecision { Grade = CellGrade.C, Reason = "C级品" };
    }
}
