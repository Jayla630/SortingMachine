// =========================================================
// File: SortingMachine.Tests/Domain/SortingServiceTests.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Codex
// =========================================================

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Tests.Helpers;
using Xunit;

namespace SortingMachine.Tests.Domain;

public sealed class SortingServiceTests : IClassFixture<SortingServiceFixture>
{
    private readonly SortingServiceFixture _fixture;

    public SortingServiceTests(SortingServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IsReadyAsync_WhenRecipeNotLoaded_ShouldReturnFalse()
    {
        // Arrange
        _fixture.SortingService.UnloadRecipe();

        // Act
        var isReady = await _fixture.SortingService.IsReadyAsync();

        // Assert
        isReady.Should().BeFalse();
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task IsReadyAsync_WhenRecipeLoadedAndAllAxesHomed_ShouldReturnTrue()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();

        // Act
        var isReady = await _fixture.SortingService.IsReadyAsync();

        // Assert
        isReady.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IsReadyAsync_WhenAnyAxisNotHomed_ShouldReturnFalse()
    {
        // Arrange
        var controller = new MockMotionController();
        await controller.InitializeAsync();
        foreach (var axis in Enum.GetValues<AxisId>())
        {
            await controller.EnableAxisAsync(axis);
        }

        var service = CreateService(controller, CreateSafeSafetyValidator().Object);
        service.LoadRecipe(CreateDefaultRecipe());

        // Act
        var isReady = await service.IsReadyAsync();

        // Assert
        isReady.Should().BeFalse();

        await controller.DisconnectAsync();
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenTypicalAGradeCell_ShouldReturnSuccessForBinA()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        var measurement = CreateMeasurement("CELL-A-001", 3800, 15);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Grade.Should().Be(CellGrade.A);
        result.BinId.Should().Be("BIN-A");
        result.GradeDecision.Should().NotBeNull();
        result.GradeDecision!.Grade.Should().Be(CellGrade.A);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenTypicalBGradeCell_ShouldReturnSuccessForBinB()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        var measurement = CreateMeasurement("CELL-B-001", 3500, 30);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Grade.Should().Be(CellGrade.B);
        result.BinId.Should().Be("BIN-B");
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenNgCell_ShouldReturnSuccessForNgBin()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        var measurement = CreateMeasurement("CELL-NG-001", 2000, 10);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Grade.Should().Be(CellGrade.NG);
        result.BinId.Should().Be("BIN-NG");
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSuccessful_ShouldIncrementTargetBinCount()
    {
        // Arrange
        var recipe = _fixture.LoadDefaultRecipe();
        var binBefore = recipe.Bins.Single(b => b.BinId == "BIN-A");
        var countBefore = binBefore.CurrentCount;
        var measurement = CreateMeasurement("CELL-A-COUNT", 3800, 15);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);
        var binAfter = recipe.Bins.Single(b => b.BinId == "BIN-A");

        // Assert
        result.IsSuccess.Should().BeTrue();
        binAfter.CurrentCount.Should().Be(countBefore + 1);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSuccessful_ShouldRaiseSortingCompletedWithInputCellId()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        SortingCompletedEventArgs? captured = null;
        _fixture.SortingService.SortingCompleted += (_, args) => captured = args;
        var measurement = CreateMeasurement("CELL-EVENT-001", 3800, 15);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Result.CellId.Should().Be(measurement.CellId);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSuccessful_ShouldReturnPositiveDuration()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        var measurement = CreateMeasurement("CELL-DURATION", 3800, 15);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenRecipeNotLoaded_ShouldReturnFailure()
    {
        // Arrange
        _fixture.SortingService.UnloadRecipe();
        var measurement = CreateMeasurement("CELL-NORECIPE", 3800, 15);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenControllerNotHomed_ShouldReturnFailure()
    {
        // Arrange
        var controller = new MockMotionController();
        await controller.InitializeAsync();
        foreach (var axis in Enum.GetValues<AxisId>())
        {
            await controller.EnableAxisAsync(axis);
        }

        var service = CreateService(controller, CreateSafeSafetyValidator().Object);
        service.LoadRecipe(CreateDefaultRecipe());
        var measurement = CreateMeasurement("CELL-NOTHOMED", 3800, 15);

        // Act
        var result = await service.SortCellAsync(measurement);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await controller.DisconnectAsync();
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenBinReachesMaxCapacity_ShouldRaiseBinFull()
    {
        // Arrange
        var recipe = _fixture.LoadDefaultRecipe();
        BinFullEventArgs? captured = null;
        _fixture.SortingService.BinFull += (_, args) => captured = args;
        var maxCapacity = recipe.Bins.Single(b => b.BinId == "BIN-A").MaxCapacity;

        // Act
        SortingResult? lastResult = null;
        for (var i = 0; i < maxCapacity; i++)
        {
            lastResult = await _fixture.SortingService.SortCellAsync(
                CreateMeasurement($"CELL-FULL-{i}", 3800, 15));
        }

        // Assert
        lastResult.Should().NotBeNull();
        lastResult!.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenBinAlreadyFull_ShouldReturnFailure()
    {
        // Arrange
        var recipe = _fixture.LoadDefaultRecipe();
        var maxCapacity = recipe.Bins.Single(b => b.BinId == "BIN-A").MaxCapacity;
        for (var i = 0; i < maxCapacity; i++)
        {
            await _fixture.SortingService.SortCellAsync(CreateMeasurement($"CELL-PREFILL-{i}", 3800, 15));
        }

        // Act
        var result = await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-OVERFLOW", 3800, 15));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("BIN-A");
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenBinFullEventRaised_ShouldIncludeGradeAndBinId()
    {
        // Arrange
        var recipe = _fixture.LoadDefaultRecipe();
        BinFullEventArgs? captured = null;
        _fixture.SortingService.BinFull += (_, args) => captured = args;
        var maxCapacity = recipe.Bins.Single(b => b.BinId == "BIN-A").MaxCapacity;

        // Act
        for (var i = 0; i < maxCapacity; i++)
        {
            await _fixture.SortingService.SortCellAsync(CreateMeasurement($"CELL-FULL-EVENT-{i}", 3800, 15));
        }

        // Assert
        captured.Should().NotBeNull();
        captured!.Grade.Should().Be(CellGrade.A);
        captured.Bin.BinId.Should().Be("BIN-A");
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSorting_ShouldMoveZBeforeXAndY()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        var movingAxes = new List<AxisId>();
        _fixture.MockController.AxisStatusChanged += (_, args) =>
        {
            if (!args.PreviousStatus.IsMoving && args.CurrentStatus.IsMoving)
            {
                movingAxes.Add(args.CurrentStatus.AxisId);
            }
        };

        // Act
        var result = await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-ORDER", 3800, 15));

        // Assert
        result.IsSuccess.Should().BeTrue();
        movingAxes.Should().NotBeEmpty();
        movingAxes.First().Should().Be(AxisId.Z);
        movingAxes.IndexOf(AxisId.X).Should().BeGreaterThan(0);
        movingAxes.IndexOf(AxisId.Y).Should().BeGreaterThan(0);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenCompleted_ShouldReturnZToSafeHeight()
    {
        // Arrange
        var recipe = _fixture.LoadDefaultRecipe();
        var measurement = CreateMeasurement("CELL-ZSAFE", 3800, 15);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(measurement);
        var zStatus = await _fixture.MockController.GetAxisStatusAsync(AxisId.Z);

        // Assert
        result.IsSuccess.Should().BeTrue();
        zStatus.Should().BeAtPosition(recipe.MotionParameters.SafeZHeight, 0.001);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSorting_ShouldCallSafetyValidator()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();

        // Act
        var result = await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-SAFETY", 3800, 15));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fixture.MockSafetyValidator.Verify(
            v => v.ValidateMoveAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyDictionary<AxisId, AxisStatus>>()),
            Times.AtLeastOnce);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSafetyValidatorReturnsUnsafe_ShouldReturnFailureAndNotMove()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        _fixture.MockSafetyValidator
            .Setup(v => v.ValidateMoveAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyDictionary<AxisId, AxisStatus>>()))
            .ReturnsAsync(SafetyCheckResult.Unsafe(SafetyViolationType.CollisionZone, "blocked"));
        var before = await GetStatusesAsync(_fixture.MockController);

        // Act
        var result = await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-UNSAFE", 3800, 15));
        var after = await GetStatusesAsync(_fixture.MockController);

        // Assert
        result.IsSuccess.Should().BeFalse();
        after[AxisId.X].Position.Should().BeApproximately(before[AxisId.X].Position, 0.001);
        after[AxisId.Y].Position.Should().BeApproximately(before[AxisId.Y].Position, 0.001);
        after[AxisId.Z].Position.Should().BeApproximately(before[AxisId.Z].Position, 0.001);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenSafetyValidatorReturnsUnsafe_ShouldNotRaiseSortingCompleted()
    {
        // Arrange
        _fixture.LoadDefaultRecipe();
        SortingCompletedEventArgs? captured = null;
        _fixture.SortingService.SortingCompleted += (_, args) => captured = args;
        _fixture.MockSafetyValidator
            .Setup(v => v.ValidateMoveAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyDictionary<AxisId, AxisStatus>>()))
            .ReturnsAsync(SafetyCheckResult.Unsafe(SafetyViolationType.CollisionZone, "blocked"));

        // Act
        var result = await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-UNSAFE-EVENT", 3800, 15));

        // Assert
        result.IsSuccess.Should().BeFalse();
        captured.Should().BeNull();
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenCancellationRequestedDuringMotion_ShouldReturnFailureOrThrowCancellation()
    {
        // Arrange
        var localFixture = await CreateReadyFixtureAsync();
        using var cts = new CancellationTokenSource();

        // Act
        var sortTask = localFixture.SortingService.SortCellAsync(CreateMeasurement("CELL-CANCEL", 3800, 15), cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();
        var act = async () => await sortTask;

        // Assert
        try
        {
            var result = await act();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        }
        catch (OperationCanceledException exception)
        {
            exception.Should().BeOfType<OperationCanceledException>();
        }
        finally
        {
            await localFixture.DisposeAsync();
        }
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenCancellationRequested_ShouldLeaveNoAxisMoving()
    {
        // Arrange
        var localFixture = await CreateReadyFixtureAsync();
        using var cts = new CancellationTokenSource();

        // Act
        var sortTask = localFixture.SortingService.SortCellAsync(CreateMeasurement("CELL-CANCEL-STATUS", 3800, 15), cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();
        try
        {
            await sortTask;
        }
        catch (OperationCanceledException)
        {
        }

        var statuses = await GetStatusesAsync(localFixture.MockController);

        // Assert
        statuses.Values.Should().OnlyContain(status => !status.IsMoving);

        await localFixture.DisposeAsync();
    }

    [Fact(Timeout = 20000)]
    [Trait("Category", "Unit")]
    public async Task SortCellAsync_WhenTwoSortsStartTogether_ShouldSerializeBothOperations()
    {
        // Arrange
        var localFixture = await CreateReadyFixtureAsync();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var first = localFixture.SortingService.SortCellAsync(CreateMeasurement("CELL-CONCURRENT-1", 3800, 15));
        await Task.Delay(10);
        var second = localFixture.SortingService.SortCellAsync(CreateMeasurement("CELL-CONCURRENT-2", 3500, 30));
        var results = await Task.WhenAll(first, second);
        stopwatch.Stop();

        // Assert
        results.Should().OnlyContain(result => result.IsSuccess);
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(900));

        await localFixture.DisposeAsync();
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ResetBinCounts_WhenBinsHaveCounts_ShouldResetAllCountsToZero()
    {
        // Arrange
        var recipe = _fixture.LoadDefaultRecipe();
        await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-RESET-A", 3800, 15));
        await _fixture.SortingService.SortCellAsync(CreateMeasurement("CELL-RESET-B", 3500, 30));
        recipe.Bins.Should().Contain(bin => bin.CurrentCount > 0);

        // Act
        _fixture.SortingService.ResetBinCounts();

        // Assert
        recipe.Bins.Should().OnlyContain(bin => bin.CurrentCount == 0);
    }

    private static async Task<SortingServiceFixture> CreateReadyFixtureAsync()
    {
        var fixture = new SortingServiceFixture();
        await fixture.InitializeAsync();
        fixture.LoadDefaultRecipe();
        return fixture;
    }

    private static ISortingService CreateService(
        IMotionController controller,
        ISafetyValidator safetyValidator)
    {
        return new SortingService(
            controller,
            safetyValidator,
            NullLogger<SortingService>.Instance);
    }

    private static Mock<ISafetyValidator> CreateSafeSafetyValidator()
    {
        var safetyValidator = new Mock<ISafetyValidator>();
        safetyValidator
            .Setup(v => v.ValidateMoveAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyDictionary<AxisId, AxisStatus>>()))
            .ReturnsAsync(SafetyCheckResult.Safe());
        return safetyValidator;
    }

    private static SortingRecipe CreateDefaultRecipe()
    {
        return new SortingRecipe
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
    }

    private static CellMeasurement CreateMeasurement(string cellId, double ocvVoltage, double irResistance)
    {
        return new CellMeasurement
        {
            CellId = cellId,
            OcvVoltage = ocvVoltage,
            IrResistance = irResistance,
            TestStation = "TEST-STATION"
        };
    }

    private static async Task<IReadOnlyDictionary<AxisId, AxisStatus>> GetStatusesAsync(
        IMotionController controller)
    {
        return new Dictionary<AxisId, AxisStatus>
        {
            [AxisId.X] = await controller.GetAxisStatusAsync(AxisId.X),
            [AxisId.Y] = await controller.GetAxisStatusAsync(AxisId.Y),
            [AxisId.Z] = await controller.GetAxisStatusAsync(AxisId.Z)
        };
    }
}

// Total tests in this file: 23
// Coverage: Ready / SuccessPath / PreCondition / BinFull / MotionOrder / Safety / Cancel / Concurrency
