// =========================================================
// File: Infrastructure/Persistence/RecipeRepository.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Gemini CLI
// =========================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FreeSql;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain.Recipe;

namespace SortingMachine.Infrastructure.Persistence;

/// <summary>
/// FreeSql 实体（仅用于 ORM 映射，不对外暴露）
/// </summary>
[Table(Name = "sorting_recipes")]
internal class RecipeEntity
{
    [Column(IsPrimary = true, StringLength = 50)]
    public string RecipeId { get; set; } = string.Empty;

    [Column(StringLength = 100)]
    public string ProductModel { get; set; } = string.Empty;

    [Column(DbType = "TEXT")]
    public string RecipeJson { get; set; } = string.Empty;   // 完整配方 JSON

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class RecipeRepository : IRecipeRepository
{
    private readonly IFreeSql _fsql;
    private readonly ILogger<RecipeRepository> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RecipeRepository(IFreeSql fsql, ILogger<RecipeRepository> logger)
    {
        _fsql = fsql;
        _logger = logger;
    }

    public async Task<SortingRecipe?> GetByIdAsync(string recipeId)
    {
        var entity = await _fsql.Select<RecipeEntity>()
            .Where(a => a.RecipeId == recipeId)
            .ToOneAsync();
        return MapToDomain(entity);
    }

    public async Task<IEnumerable<SortingRecipe>> GetAllAsync()
    {
        var entities = await _fsql.Select<RecipeEntity>()
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();
        return entities.Select(MapToDomain).Where(r => r != null)!;
    }

    public async Task<SortingRecipe?> GetActiveAsync()
    {
        var entity = await _fsql.Select<RecipeEntity>()
            .Where(a => a.IsActive)
            .ToOneAsync();
        return MapToDomain(entity);
    }

    public async Task SaveAsync(SortingRecipe recipe)
    {
        var entity = new RecipeEntity
        {
            RecipeId = recipe.RecipeId,
            ProductModel = recipe.ProductModel,
            RecipeJson = JsonSerializer.Serialize(recipe, JsonOptions),
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = DateTime.Now,
            IsActive = recipe.IsActive
        };

        await _fsql.InsertOrUpdate<RecipeEntity>()
            .SetSource(entity)
            .ExecuteAffrowsAsync();
        
        _logger.LogInformation("Recipe {RecipeId} saved.", recipe.RecipeId);
    }

    public async Task DeleteAsync(string recipeId)
    {
        await _fsql.Delete<RecipeEntity>()
            .Where(a => a.RecipeId == recipeId)
            .ExecuteAffrowsAsync();
        
        _logger.LogInformation("Recipe {RecipeId} deleted.", recipeId);
    }

    public async Task SetActiveAsync(string recipeId)
    {
        try
        {
            // 1. 清除所有激活状态
            await _fsql.Update<RecipeEntity>()
                .Set(a => a.IsActive, false)
                .Where(a => a.IsActive)
                .ExecuteAffrowsAsync();

            // 2. 激活目标配方
            await _fsql.Update<RecipeEntity>()
                .Set(a => a.IsActive, true)
                .Where(a => a.RecipeId == recipeId)
                .ExecuteAffrowsAsync();

            _logger.LogInformation("Recipe {RecipeId} activated.", recipeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate recipe {RecipeId}", recipeId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string recipeId)
    {
        return await _fsql.Select<RecipeEntity>()
            .Where(a => a.RecipeId == recipeId)
            .AnyAsync();
    }

    private SortingRecipe? MapToDomain(RecipeEntity? entity)
    {
        if (entity == null) return null;
        try
        {
            var recipe = JsonSerializer.Deserialize<SortingRecipe>(entity.RecipeJson, JsonOptions);
            if (recipe != null)
            {
                // 确保 Entity 中的关键状态同步到 Domain 对象（如果 JSON 中不包含或已过期）
                return recipe with { IsActive = entity.IsActive };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize recipe {RecipeId}", entity.RecipeId);
        }
        return null;
    }
}
