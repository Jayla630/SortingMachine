// =========================================================
// File: Infrastructure/Persistence/DatabaseInitializer.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Gemini CLI
// =========================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain.Recipe;

namespace SortingMachine.Infrastructure.Persistence;

public class DatabaseInitializer
{
    private readonly IFreeSql _fsql;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IFreeSql fsql, IRecipeRepository recipeRepository, ILogger<DatabaseInitializer> logger)
    {
        _fsql = fsql;
        _recipeRepository = recipeRepository;
        _logger = logger;
    }

    /// <summary>
    /// 初始化数据库：建表 + 注入默认配方（仅首次运行）
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing database...");

            // 1. CodeFirst 建表
            _fsql.CodeFirst.SyncStructure<RecipeEntity>();

            // 2. 如果没有任何配方，插入一条默认种子配方
            if (!await _fsql.Select<RecipeEntity>().AnyAsync())
            {
                _logger.LogInformation("No recipes found. Seeding default recipe...");
                var defaultRecipe = CreateDefaultRecipe();
                await _recipeRepository.SaveAsync(defaultRecipe);
                await _recipeRepository.SetActiveAsync(defaultRecipe.RecipeId);
            }

            _logger.LogInformation("Database initialization completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }

    private SortingRecipe CreateDefaultRecipe()
    {
        return new SortingRecipe
        {
            RecipeId = "DEFAULT01",
            ProductModel = "INR21700-50E（示例）",
            Description = "系统自动生成的默认配方",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsActive = true,
            Bins = new List<BinDefinition>
            {
                new BinDefinition { BinId = "BIN-A",  TargetGrade = CellGrade.A,  X = 100, Y = 50, ZPickHeight = 30, MaxCapacity = 50 },
                new BinDefinition { BinId = "BIN-B",  TargetGrade = CellGrade.B,  X = 200, Y = 50, ZPickHeight = 30, MaxCapacity = 50 },
                new BinDefinition { BinId = "BIN-C",  TargetGrade = CellGrade.C,  X = 300, Y = 50, ZPickHeight = 30, MaxCapacity = 50 },
                new BinDefinition { BinId = "BIN-NG", TargetGrade = CellGrade.NG, X = 400, Y = 50, ZPickHeight = 30, MaxCapacity = 200 }
            },
            GradingRules = new GradingRules(), // 使用默认阈值
            MotionParameters = new MotionParameters
            {
                XyVelocity = 100,
                ZVelocity = 50,
                SafeZHeight = 0
            }
        };
    }
}
