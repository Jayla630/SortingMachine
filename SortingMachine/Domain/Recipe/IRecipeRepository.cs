// =========================================================
// File: Domain/Recipe/IRecipeRepository.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
namespace SortingMachine.Domain.Recipe;

/// <summary>
/// 配方仓储契约
/// TODO: Sprint S4 使用 FreeSql + SQLite 实现
/// </summary>
public interface IRecipeRepository
{
    Task<SortingRecipe?> GetByIdAsync(string recipeId);
    Task<IEnumerable<SortingRecipe>> GetAllAsync();
    Task SaveAsync(SortingRecipe recipe);
    Task DeleteAsync(string recipeId);
}
