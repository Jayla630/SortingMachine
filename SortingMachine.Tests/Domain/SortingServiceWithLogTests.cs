// =========================================================
// File: SortingMachine.Tests/Domain/SortingServiceWithLogTests.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Codex
// =========================================================

using FluentAssertions;
using FreeSql;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Infrastructure.Persistence;
using Xunit;

namespace SortingMachine.Tests.Domain;

public sealed class SortingServiceWithLogTests
{
    [Fact(Timeout = 5000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenSuccessful_ShouldCallSortingLogRepositorySaveOnce()
    {
        // Arrange
        var logRepository = new Mock<ISortingLogRepository>();
        logRepository.Setup(r => r.SaveAsync(It.IsAny<SortingLog>())).Returns(Task.CompletedTask);
        var service = CreateServiceWithMockMotion(logRepository.Object);
        service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await service.SortCellAsync(CreateMeasurement("CELL-SL001", 3800, 15));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        logRepository.Verify(r => r.SaveAsync(It.IsAny<SortingLog>()), Times.Once);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenServiceIsNotReady_ShouldNotCallSortingLogRepositorySave()
    {
        // Arrange
        var logRepository = new Mock<ISortingLogRepository>();
        logRepository.Setup(r => r.SaveAsync(It.IsAny<SortingLog>())).Returns(Task.CompletedTask);
        var service = CreateServiceWithMockMotion(logRepository.Object);

        // Act
        var result = await service.SortCellAsync(CreateMeasurement("CELL-SL002", 3800, 15));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        logRepository.Verify(r => r.SaveAsync(It.IsAny<SortingLog>()), Times.Never);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenSaveAsyncThrows_ShouldStillReturnSuccessfulSortingResult()
    {
        // Arrange
        var logRepository = new Mock<ISortingLogRepository>();
        logRepository
            .Setup(r => r.SaveAsync(It.IsAny<SortingLog>()))
            .Returns(Task.FromException(new InvalidOperationException("log write failed")));
        var service = CreateServiceWithMockMotion(logRepository.Object);
        service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await service.SortCellAsync(CreateMeasurement("CELL-SL003", 3800, 15));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CellId.Should().Be("CELL-SL003");
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenSuccessful_ShouldWriteLogWithInputCellId()
    {
        // Arrange
        SortingLog? captured = null;
        var logRepository = CreateCapturingLogRepository(log => captured = log);
        var service = CreateServiceWithMockMotion(logRepository.Object);
        service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await service.SortCellAsync(CreateMeasurement("CELL-SL004", 3800, 15));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.CellId.Should().Be("CELL-SL004");
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenSuccessful_ShouldWriteLogWithResultGrade()
    {
        // Arrange
        SortingLog? captured = null;
        var logRepository = CreateCapturingLogRepository(log => captured = log);
        var service = CreateServiceWithMockMotion(logRepository.Object);
        service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await service.SortCellAsync(CreateMeasurement("CELL-SL005", 3500, 30));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Grade.Should().Be(CellGrade.B);
        captured.Should().NotBeNull();
        captured!.Grade.Should().Be(result.Grade.ToString());
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenSuccessful_ShouldWriteLogWithResultSuccessFlag()
    {
        // Arrange
        SortingLog? captured = null;
        var logRepository = CreateCapturingLogRepository(log => captured = log);
        var service = CreateServiceWithMockMotion(logRepository.Object);
        service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await service.SortCellAsync(CreateMeasurement("CELL-SL006", 3800, 15));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.IsSuccess.Should().Be(result.IsSuccess);
    }

    [Fact(Timeout = 12000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenUsingRealMotionAndRepository_ShouldWriteLogToSqlite()
    {
        // Arrange
        await using var fixture = await SortingServiceLogFixture.CreateReadyAsync();
        fixture.Service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await fixture.Service.SortCellAsync(CreateMeasurement("CELL-SL010", 3800, 15));
        await WaitForLogWriteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var count = await fixture.Repository.GetTotalCountAsync();
        count.Should().Be(1);
    }

    [Fact(Timeout = 12000)]
    [Trait("Category", "Integration")]
    public async Task GetRecentAsync_WhenServiceWritesLog_ShouldReturnJustWrittenCellId()
    {
        // Arrange
        await using var fixture = await SortingServiceLogFixture.CreateReadyAsync();
        fixture.Service.LoadRecipe(CreateFastRecipe());

        // Act
        var result = await fixture.Service.SortCellAsync(CreateMeasurement("CELL-SL011", 3800, 15));
        await WaitForLogWriteAsync();
        var recent = (await fixture.Repository.GetRecentAsync(1)).ToList();

        // Assert
        result.IsSuccess.Should().BeTrue();
        recent.Should().ContainSingle();
        recent[0].CellId.Should().Be("CELL-SL011");
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Integration")]
    public async Task SortCellAsync_WhenSortingFiveCells_ShouldWriteFiveLogs()
    {
        // Arrange
        await using var fixture = await SortingServiceLogFixture.CreateReadyAsync();
        fixture.Service.LoadRecipe(CreateFastRecipe());

        // Act
        for (var i = 0; i < 5; i++)
        {
            var result = await fixture.Service.SortCellAsync(CreateMeasurement($"CELL-SL012-{i}", 3800, 15));
            result.IsSuccess.Should().BeTrue();
        }
        await WaitForLogWriteAsync();

        // Assert
        var count = await fixture.Repository.GetTotalCountAsync();
        count.Should().Be(5);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Integration")]
    public async Task GetGradeStatisticsAsync_WhenServiceWritesMultipleGrades_ShouldMatchSortingResults()
    {
        // Arrange
        await using var fixture = await SortingServiceLogFixture.CreateReadyAsync();
        fixture.Service.LoadRecipe(CreateFastRecipe());
        var measurements = new[]
        {
            CreateMeasurement("CELL-SL013-A1", 3800, 15),
            CreateMeasurement("CELL-SL013-A2", 3900, 18),
            CreateMeasurement("CELL-SL013-B", 3500, 30),
            CreateMeasurement("CELL-SL013-C", 3300, 40),
            CreateMeasurement("CELL-SL013-NG", 3000, 10)
        };
        var expectedCounts = new Dictionary<string, int>();

        // Act
        foreach (var measurement in measurements)
        {
            var result = await fixture.Service.SortCellAsync(measurement);
            result.IsSuccess.Should().BeTrue();
            expectedCounts[result.Grade.ToString()] = expectedCounts.GetValueOrDefault(result.Grade.ToString()) + 1;
        }
        await WaitForLogWriteAsync();
        var statistics = await fixture.Repository.GetGradeStatisticsAsync();

        // Assert
        statistics.Should().BeEquivalentTo(expectedCounts);
    }

    private static Mock<ISortingLogRepository> CreateCapturingLogRepository(Action<SortingLog> capture)
    {
        var repository = new Mock<ISortingLogRepository>();
        repository
            .Setup(r => r.SaveAsync(It.IsAny<SortingLog>()))
            .Callback<SortingLog>(capture)
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static SortingService CreateServiceWithMockMotion(ISortingLogRepository logRepository)
    {
        var motion = new Mock<IMotionController>();
        var readyStatus = new AxisStatus
        {
            AxisId = AxisId.X,
            IsEnabled = true,
            IsHomed = true,
            IsMoving = false,
            HasAlarm = false,
            Timestamp = DateTime.UtcNow
        };
        motion.Setup(m => m.IsAllAxesReadyAsync()).ReturnsAsync(true);
        motion.Setup(m => m.GetAxisStatusAsync(It.IsAny<AxisId>()))
            .ReturnsAsync((AxisId axis) => readyStatus with { AxisId = axis });
        motion.Setup(m => m.MoveAbsoluteAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MotionResult.Ok());

        var safety = new Mock<ISafetyValidator>();
        safety
            .Setup(v => v.ValidateMoveAsync(
                It.IsAny<AxisId>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyDictionary<AxisId, AxisStatus>>()))
            .ReturnsAsync(SafetyCheckResult.Safe());

        return new SortingService(
            motion.Object,
            safety.Object,
            logRepository,
            NullLogger<SortingService>.Instance);
    }

    private static SortingRecipe CreateFastRecipe()
        => new()
        {
            RecipeId = "TEST001",
            ProductModel = "TestProduct",
            Bins = new List<BinDefinition>
            {
                new() { BinId = "BIN-A", TargetGrade = CellGrade.A, X = 100, Y = 50, ZPickHeight = 30, MaxCapacity = 20 },
                new() { BinId = "BIN-B", TargetGrade = CellGrade.B, X = 200, Y = 50, ZPickHeight = 30, MaxCapacity = 20 },
                new() { BinId = "BIN-C", TargetGrade = CellGrade.C, X = 300, Y = 50, ZPickHeight = 30, MaxCapacity = 20 },
                new() { BinId = "BIN-NG", TargetGrade = CellGrade.NG, X = 400, Y = 50, ZPickHeight = 30, MaxCapacity = 20 }
            },
            GradingRules = new GradingRules(),
            MotionParameters = new MotionParameters
            {
                XyVelocity = 50_000,
                ZVelocity = 50_000,
                SafeZHeight = 0
            }
        };

    private static CellMeasurement CreateMeasurement(string cellId, double ocvVoltage, double irResistance)
        => new()
        {
            CellId = cellId,
            OcvVoltage = ocvVoltage,
            IrResistance = irResistance,
            TestStation = "TEST-STATION"
        };

    private static async Task WaitForLogWriteAsync(int ms = 200)
        => await Task.Delay(ms);
}

public sealed class SortingServiceLogFixture : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly IFreeSql _fsql;

    public MockMotionController MotionController { get; }
    public ISortingLogRepository Repository { get; }
    public SortingService Service { get; }

    private SortingServiceLogFixture(string dbPath, IFreeSql fsql, MockMotionController motionController)
    {
        _dbPath = dbPath;
        _fsql = fsql;
        MotionController = motionController;
        Repository = new SortingLogRepository(_fsql, NullLogger<SortingLogRepository>.Instance);
        Service = new SortingService(
            MotionController,
            CreateSafeSafetyValidator().Object,
            Repository,
            NullLogger<SortingService>.Instance);
    }

    public static async Task<SortingServiceLogFixture> CreateReadyAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sorting_service_test_{Guid.NewGuid():N}.db");
        var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={dbPath};")
            .UseAutoSyncStructure(true)
            .Build();
        var motionController = new MockMotionController();
        await motionController.InitializeAsync();
        foreach (var axis in Enum.GetValues<AxisId>())
        {
            await motionController.EnableAxisAsync(axis);
        }
        await Task.WhenAll(Enum.GetValues<AxisId>().Select(axis => motionController.HomeAsync(axis)));

        return new SortingServiceLogFixture(dbPath, fsql, motionController);
    }

    public async ValueTask DisposeAsync()
    {
        await MotionController.DisconnectAsync();
        _fsql.Dispose();
        await Task.Delay(100);
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
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
}

// Total tests in this file: 10
// Coverage: Integration / LogWrite / RepositoryFailure / RealSqlite
