// =========================================================
// File: Domain/Recipe/BinDefinition.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain.Recipe;

/// <summary>
/// 料仓定义 —— 位置坐标、目标等级、容量限制。
/// 每个料仓对应一个 CellGrade，机械手运动到 (X, Y) 位置后下降放料。
/// </summary>
public record BinDefinition
{
    public string BinId { get; init; } = string.Empty;     // "BIN-A1", "BIN-NG" 等
    public CellGrade TargetGrade { get; init; }             // 此料仓接收哪个等级
    public double X { get; init; }                          // 料仓 X 坐标 mm
    public double Y { get; init; }                          // 料仓 Y 坐标 mm
    public double ZPickHeight { get; init; }                // 放料时 Z 轴下降到的高度 mm
    public int MaxCapacity { get; init; } = 50;             // 满仓阈值（颗）
    public int CurrentCount { get; set; } = 0;              // 当前已放数量（运行时可变）

    public bool IsFull => CurrentCount >= MaxCapacity;
}
