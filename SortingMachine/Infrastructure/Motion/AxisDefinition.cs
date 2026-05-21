// =========================================================
// File: Infrastructure/Motion/AxisDefinition.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Infrastructure.Motion;

/// <summary>
/// 轴标识枚举 —— 强类型替代雷赛 SDK 的 int 轴号。
/// </summary>
public enum AxisId
{
    X = 0,   // 龙门 X 轴（横向定位，对应料仓列）
    Y = 1,   // 龙门 Y 轴（纵向定位，对应料仓行）
    Z = 2    // 吸嘴升降轴（取料/放料动作）
}

/// <summary>
/// 轴配置值对象 —— 软限位范围及原点偏置。
/// Mock 使用此配置初始化各轴限位；真实驱动层后续 Sprint 从硬件参数或配置文件读取。
/// </summary>
public sealed record AxisConfig
{
    public AxisId AxisId { get; init; }
    public double NegativeLimit { get; init; }
    public double PositiveLimit { get; init; }
    public double OriginOffset { get; init; }

    public static AxisConfig DefaultX => new()
    {
        AxisId = AxisId.X,
        NegativeLimit = 0,
        PositiveLimit = 500,
        OriginOffset = 0
    };

    public static AxisConfig DefaultY => new()
    {
        AxisId = AxisId.Y,
        NegativeLimit = 0,
        PositiveLimit = 400,
        OriginOffset = 0
    };

    public static AxisConfig DefaultZ => new()
    {
        AxisId = AxisId.Z,
        NegativeLimit = 0,
        PositiveLimit = 150,
        OriginOffset = 0
    };
}
