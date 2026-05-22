// =========================================================
// File: Domain/SortingLog.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Claude Code
// =========================================================

using SortingMachine.Domain.Recipe;

namespace SortingMachine.Domain;

/// <summary>
/// 单颗电芯的完整分选记录，用于追溯和 MES 上报。
/// 生活类比：快递分拣员的手写台账，每颗电芯的去向都有据可查。
/// </summary>
public record SortingLog
{
    public long Id { get; init; }
    public string CellId { get; init; } = string.Empty;
    public double OcvVoltage { get; init; }
    public double IrResistance { get; init; }
    public string Grade { get; init; } = string.Empty;
    public string GradeReason { get; init; } = string.Empty;
    public string? TriggeringMetric { get; init; }
    public string? BinId { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public double DurationMs { get; init; }
    public string? RecipeId { get; init; }
    public string? ProductModel { get; init; }
    public DateTime SortedAt { get; init; } = DateTime.Now;

    /// <summary>从 SortingResult 和 CellMeasurement 构建日志记录</summary>
    public static SortingLog FromResult(
        SortingResult result,
        CellMeasurement measurement,
        string? recipeId,
        string? productModel)
        => new()
        {
            CellId = result.CellId,
            OcvVoltage = measurement.OcvVoltage,
            IrResistance = measurement.IrResistance,
            Grade = result.Grade.ToString(),
            GradeReason = result.GradeDecision?.Reason ?? string.Empty,
            TriggeringMetric = result.GradeDecision?.TriggeringMetric,
            BinId = result.BinId,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            DurationMs = result.Duration.TotalMilliseconds,
            RecipeId = recipeId,
            ProductModel = productModel,
            SortedAt = result.SortedAt
        };
}
