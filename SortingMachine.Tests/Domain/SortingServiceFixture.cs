// =========================================================
// File: SortingMachine.Tests/Domain/SortingServiceFixture.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Codex
// =========================================================

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.Motion;
using Xunit;

namespace SortingMachine.Tests.Domain;

public sealed class SortingServiceFixture : IAsyncLifetime
{
    public MockMotionController MockController { get; private set; } = null!;
    public Mock<ISafetyValidator> MockSafetyValidator { get; private set; } = null!;
    public ISortingService SortingService { get; private set; } = null!;

    public SortingRecipe DefaultRecipe { get; } = new()
    {
        RecipeId = "TEST001",
        ProductModel = "TestProduct",
        Bins = new List<BinDefinition>
        {
            new() { BinId = "BIN-A", TargetGrade = CellGrade.A, X = 100, Y = 50, ZPickHeight = 30, MaxCapacity = 5 },
            new() { BinId = "BIN-B", TargetGrade = CellGrade.B, X = 200, Y = 50, ZPickHeight = 30, MaxCapacity = 5 },
            new() { BinId = "BIN-C", TargetGrade = CellGrade.C, X = 300, Y = 50, ZPickHeight = 30, MaxCapacity = 5 },
            new() { BinId = "BIN-NG", TargetGrade = CellGrade.NG, X = 400, Y = 50, ZPickHeight = 30, MaxCapacity = 20 }
        },
        GradingRules = new GradingRules(),
        MotionParameters = new MotionParameters { XyVelocity = 500, ZVelocity = 500 }
    };

    public async Task InitializeAsync()
    {
        MockController = new MockMotionController();
        await MockController.InitializeAsync();
        foreach (var axis in Enum.GetValues<AxisId>())
        {
            await MockController.EnableAxisAsync(axis);
            await MockController.HomeAsync(axis);
        }

        MockSafetyValidator = new Mock<ISafetyValidator>();
        ResetSafetyValidatorToSafe();

        SortingService = new SortingService(
            MockController,
            MockSafetyValidator.Object,
            NullLogger<SortingService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await MockController.DisconnectAsync();
    }

    public SortingRecipe LoadDefaultRecipe()
    {
        ResetSafetyValidatorToSafe();
        var recipe = CloneDefaultRecipe();
        SortingService.LoadRecipe(recipe);
        return recipe;
    }

    public void ResetSafetyValidatorToSafe()
    {
        MockSafetyValidator.Reset();
        MockSafetyValidator
            .Setup(v => v.ValidateMoveAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyDictionary<AxisId, AxisStatus>>()))
            .ReturnsAsync(SafetyCheckResult.Safe());
    }

    private SortingRecipe CloneDefaultRecipe()
    {
        return DefaultRecipe with
        {
            Bins = DefaultRecipe.Bins
                .Select(bin => bin with { CurrentCount = 0 })
                .ToList(),
            GradingRules = DefaultRecipe.GradingRules,
            MotionParameters = DefaultRecipe.MotionParameters
        };
    }
}

// Total tests in this file: 0
// Coverage: Fixture / RecipeBuilder / SafetyMock
