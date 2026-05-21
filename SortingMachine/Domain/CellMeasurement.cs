// =========================================================
// File: Domain/CellMeasurement.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain;

/// <summary>
/// 电芯检测数据 —— 来自 OCV/IR 测试工位的输入。
/// </summary>
public record CellMeasurement
{
    public string CellId { get; init; } = string.Empty;       // 电芯条码
    public double OcvVoltage { get; init; }                    // OCV 电压 mV
    public double IrResistance { get; init; }                  // IR 内阻 mΩ
    public DateTime MeasuredAt { get; init; } = DateTime.Now;  // 检测时间戳
    public string? TestStation { get; init; }                  // 来源检测工位（可空）
}
