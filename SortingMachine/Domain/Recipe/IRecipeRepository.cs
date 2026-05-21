// =========================================================
// File: Domain/Recipe/IRecipeRepository.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Gemini CLI
// =========================================================
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingMachine.Domain.Recipe;

/// <summary>
/// 配方仓储契约
/// </summary>
public interface IRecipeRepository
{
    Task<SortingRecipe?> GetByIdAsync(string recipeId);
    Task<IEnumerable<SortingRecipe>> GetAllAsync();
    Task<SortingRecipe?> GetActiveAsync();           // 获取当前激活配方
    Task SaveAsync(SortingRecipe recipe);            // 新增或更新（RecipeId 存在则更新）
    Task DeleteAsync(string recipeId);
    Task SetActiveAsync(string recipeId);            // 设置激活配方（同时清除其他配方的 IsActive）
    Task<bool> ExistsAsync(string recipeId);
}
