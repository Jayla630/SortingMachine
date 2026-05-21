// =========================================================
// File: Domain/Recipe/SortingRecipe.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Gemini CLI
// =========================================================
using System;
using System.Collections.Generic;

namespace SortingMachine.Domain.Recipe;

/// <summary>
/// 贴片/分选配方定义 (包含产品尺寸、坐标偏置等静态数据)
/// </summary>
public record SortingRecipe
{
    public string RecipeId { get; init; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public string ProductModel { get; init; } = string.Empty;  // 产品型号，如 "INR21700-50E"
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = false;               // 当前产线使用中的配方

    public List<BinDefinition> Bins { get; init; } = new();
    public GradingRules GradingRules { get; init; } = new();
    public MotionParameters MotionParameters { get; init; } = new();
}

public record MotionParameters
{
    public double XyVelocity { get; init; } = 100.0;    // XY 轴运动速度 mm/s
    public double ZVelocity { get; init; } = 50.0;       // Z 轴运动速度 mm/s
    public double SafeZHeight { get; init; } = 0.0;      // 安全高度（原点/缩回位）mm
}
