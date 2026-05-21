// =========================================================
// File: Domain/Recipe/SortingRecipe.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
namespace SortingMachine.Domain.Recipe;

/// <summary>
/// 贴片/分选配方定义 (包含产品尺寸、坐标偏置等静态数据)
/// TODO: Sprint S4 接入数据库
/// </summary>
public record SortingRecipe
{
    public string RecipeId { get; init; } = string.Empty;
    public string ProductModel { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    // TODO: Sprint S4 - 增加 BinPositions
    // TODO: Sprint S4 - 增加 GradingRules
}
