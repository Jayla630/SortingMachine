// =========================================================
// File: SortingMachine.Tests/Domain/SortingLogRepositoryTests.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Codex
// =========================================================

using FluentAssertions;
using FreeSql;
using Microsoft.Extensions.Logging.Abstractions;
using SortingMachine.Domain;
using SortingMachine.Domain.Recipe;
using SortingMachine.Infrastructure.Persistence;
using Xunit;

namespace SortingMachine.Tests.Domain;

public sealed class SortingLogRepositoryTests : IAsyncLifetime
{
    private SortingLogRepositoryFixture _fixture = null!;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveAsync_WhenSavingSingleLog_ShouldIncreaseTotalCountToOne()
    {
        // Arrange
        var log = CreateLog();

        // Act
        await _fixture.Repository.SaveAsync(log);

        // Assert
        var count = await _fixture.Repository.GetTotalCountAsync();
        count.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveAsync_WhenSavingMultipleLogs_ShouldMakeAllLogsQueryable()
    {
        // Arrange
        var logs = Enumerable.Range(1, 3)
            .Select(i => CreateLog(cellId: $"CELL-L002-{i}"))
            .ToList();

        // Act
        foreach (var log in logs)
        {
            await _fixture.Repository.SaveAsync(log);
        }

        // Assert
        var recent = (await _fixture.Repository.GetRecentAsync(10)).ToList();
        recent.Select(log => log.CellId).Should().BeEquivalentTo(logs.Select(log => log.CellId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FromResult_WhenCreatingSortingLog_ShouldMapAllFields()
    {
        // Arrange
        var decision = new GradeDecision
        {
            Grade = CellGrade.NG,
            Reason = "OCV 超出合格范围",
            TriggeringMetric = "OCV=3000mV"
        };
        var result = SortingResult.Success(
            "CELL-L003",
            CellGrade.NG,
            "BIN-NG",
            decision,
            TimeSpan.FromMilliseconds(456));
        var measurement = CreateMeasurement("CELL-L003", 3000, 12);

        // Act
        var log = SortingLog.FromResult(result, measurement, "RCP-L003", "Model-L003");

        // Assert
        log.CellId.Should().Be("CELL-L003");
        log.OcvVoltage.Should().Be(3000);
        log.IrResistance.Should().Be(12);
        log.Grade.Should().Be("NG");
        log.GradeReason.Should().Be(decision.Reason);
        log.TriggeringMetric.Should().Be(decision.TriggeringMetric);
        log.BinId.Should().Be("BIN-NG");
        log.IsSuccess.Should().BeTrue();
        log.ErrorMessage.Should().BeNull();
        log.DurationMs.Should().BeApproximately(456, 0.001);
        log.RecipeId.Should().Be("RCP-L003");
        log.ProductModel.Should().Be("Model-L003");
        log.SortedAt.Should().BeCloseTo(result.SortedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveAsync_WhenLogIsFailure_ShouldPersistErrorMessage()
    {
        // Arrange
        var log = CreateLog(cellId: "CELL-L004", isSuccess: false);

        // Act
        await _fixture.Repository.SaveAsync(log);

        // Assert
        var saved = await _fixture.Repository.GetByCellIdAsync("CELL-L004");
        saved.Should().NotBeNull();
        saved!.IsSuccess.Should().BeFalse();
        saved.ErrorMessage.Should().Be("测试失败");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveAsync_WhenTriggeringMetricIsNull_ShouldStoreLogWithoutError()
    {
        // Arrange
        var logs = new[]
        {
            CreateLog(cellId: "CELL-L005-A", grade: "A") with { TriggeringMetric = null },
            CreateLog(cellId: "CELL-L005-B", grade: "B") with { TriggeringMetric = null },
            CreateLog(cellId: "CELL-L005-C", grade: "C") with { TriggeringMetric = null }
        };

        // Act
        foreach (var log in logs)
        {
            await _fixture.Repository.SaveAsync(log);
        }

        // Assert
        var recent = (await _fixture.Repository.GetRecentAsync(10)).ToList();
        recent.Should().HaveCount(3);
        recent.Should().OnlyContain(log => log.TriggeringMetric == null);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByCellIdAsync_WhenCellIdExists_ShouldReturnMatchingLog()
    {
        // Arrange
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-L010"));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-OTHER"));

        // Act
        var result = await _fixture.Repository.GetByCellIdAsync("CELL-L010");

        // Assert
        result.Should().NotBeNull();
        result!.CellId.Should().Be("CELL-L010");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByCellIdAsync_WhenCellIdDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-L011"));

        // Act
        var result = await _fixture.Repository.GetByCellIdAsync("CELL-MISSING");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetRecentAsync_WhenMoreThanRequestedCount_ShouldReturnLatestTenDescending()
    {
        // Arrange
        var start = DateTime.Now.AddMinutes(-30);
        await SaveLogsAsync(12, start, i => CreateLog(cellId: $"CELL-L012-{i:00}", sortedAt: start.AddMinutes(i)));

        // Act
        var result = (await _fixture.Repository.GetRecentAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(10);
        result.Should().BeInDescendingOrder(log => log.SortedAt);
        result.Select(log => log.CellId).Should().Equal(
            Enumerable.Range(2, 10).Reverse().Select(i => $"CELL-L012-{i:00}"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetRecentAsync_WhenFewerLogsThanRequested_ShouldReturnAllLogs()
    {
        // Arrange
        await SaveLogsAsync(3, DateTime.Now, i => CreateLog(cellId: $"CELL-L013-{i}"));

        // Act
        var result = (await _fixture.Repository.GetRecentAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPagedAsync_WhenRequestingFirstPage_ShouldReturnFirstTenLogs()
    {
        // Arrange
        var start = DateTime.Now.AddMinutes(-30);
        await SaveLogsAsync(15, start, i => CreateLog(cellId: $"CELL-L014-{i:00}", sortedAt: start.AddMinutes(i)));

        // Act
        var result = (await _fixture.Repository.GetPagedAsync(0, 10)).ToList();

        // Assert
        result.Should().HaveCount(10);
        result.Select(log => log.CellId).Should().Equal(
            Enumerable.Range(5, 10).Reverse().Select(i => $"CELL-L014-{i:00}"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPagedAsync_WhenRequestingSecondPage_ShouldApplyCorrectOffset()
    {
        // Arrange
        var start = DateTime.Now.AddMinutes(-30);
        await SaveLogsAsync(15, start, i => CreateLog(cellId: $"CELL-L015-{i:00}", sortedAt: start.AddMinutes(i)));

        // Act
        var result = (await _fixture.Repository.GetPagedAsync(1, 10)).ToList();

        // Assert
        result.Should().HaveCount(5);
        result.Select(log => log.CellId).Should().Equal(
            Enumerable.Range(0, 5).Reverse().Select(i => $"CELL-L015-{i:00}"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByTimeRangeAsync_WhenLogsExistAcrossRange_ShouldReturnOnlyLogsInsideRange()
    {
        // Arrange
        var baseTime = DateTime.Now.AddHours(-1);
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-BEFORE", sortedAt: baseTime));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-IN-1", sortedAt: baseTime.AddMinutes(10)));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-IN-2", sortedAt: baseTime.AddMinutes(20)));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-AFTER", sortedAt: baseTime.AddMinutes(40)));

        // Act
        var result = (await _fixture.Repository.GetByTimeRangeAsync(
            baseTime.AddMinutes(5),
            baseTime.AddMinutes(25))).ToList();

        // Assert
        result.Select(log => log.CellId).Should().BeEquivalentTo("CELL-IN-1", "CELL-IN-2");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetGradeStatisticsAsync_WhenLogsHaveMultipleGrades_ShouldReturnCountsPerGrade()
    {
        // Arrange
        await SaveGradeLogsAsync(("A", 3), ("B", 2), ("NG", 1));

        // Act
        var result = await _fixture.Repository.GetGradeStatisticsAsync();

        // Assert
        result.Should().ContainKey("A").WhoseValue.Should().Be(3);
        result.Should().ContainKey("B").WhoseValue.Should().Be(2);
        result.Should().NotContainKey("C");
        result.Should().ContainKey("NG").WhoseValue.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetGradeStatisticsAsync_WhenSinceProvided_ShouldOnlyCountLogsAfterSince()
    {
        // Arrange
        var baseTime = DateTime.Now.AddHours(-1);
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-OLD-A", grade: "A", sortedAt: baseTime));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-NEW-A", grade: "A", sortedAt: baseTime.AddMinutes(20)));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-NEW-B", grade: "B", sortedAt: baseTime.AddMinutes(30)));

        // Act
        var result = await _fixture.Repository.GetGradeStatisticsAsync(baseTime.AddMinutes(10));

        // Assert
        result.Should().ContainKey("A").WhoseValue.Should().Be(1);
        result.Should().ContainKey("B").WhoseValue.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetGradeStatisticsAsync_WhenDatabaseIsEmpty_ShouldReturnEmptyDictionary()
    {
        // Arrange

        // Act
        var result = await _fixture.Repository.GetGradeStatisticsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPendingMesUploadAsync_WhenLogsHaveMixedSuccess_ShouldReturnOnlySuccessfulNotUploadedLogs()
    {
        // Arrange
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-L030-A", isSuccess: true));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-L030-B", isSuccess: true));
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-L030-FAIL", isSuccess: false));

        // Act
        var result = (await _fixture.Repository.GetPendingMesUploadAsync()).ToList();

        // Assert
        result.Select(log => log.CellId).Should().BeEquivalentTo("CELL-L030-A", "CELL-L030-B");
        result.Should().OnlyContain(log => log.IsSuccess);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPendingMesUploadAsync_WhenLogIsFailure_ShouldNotReturnFailedLog()
    {
        // Arrange
        await _fixture.Repository.SaveAsync(CreateLog(cellId: "CELL-L031", isSuccess: false));

        // Act
        var result = (await _fixture.Repository.GetPendingMesUploadAsync()).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    public Task InitializeAsync()
    {
        _fixture = new SortingLogRepositoryFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private async Task SaveLogsAsync(int count, DateTime start, Func<int, SortingLog> createLog)
    {
        for (var i = 0; i < count; i++)
        {
            await _fixture.Repository.SaveAsync(createLog(i));
        }
    }

    private async Task SaveGradeLogsAsync(params (string Grade, int Count)[] groups)
    {
        foreach (var (grade, count) in groups)
        {
            for (var i = 0; i < count; i++)
            {
                await _fixture.Repository.SaveAsync(CreateLog(cellId: $"CELL-{grade}-{i}", grade: grade));
            }
        }
    }

    private static SortingLog CreateLog(
        string cellId = "CELL-001",
        string grade = "A",
        bool isSuccess = true,
        DateTime? sortedAt = null)
        => new()
        {
            CellId = cellId,
            OcvVoltage = 3800,
            IrResistance = 15,
            Grade = grade,
            GradeReason = $"{grade}级品",
            TriggeringMetric = grade == "NG" ? "OCV=3000mV" : null,
            BinId = isSuccess ? $"BIN-{grade}" : null,
            IsSuccess = isSuccess,
            ErrorMessage = isSuccess ? null : "测试失败",
            DurationMs = 1200,
            RecipeId = "TEST001",
            ProductModel = "TestProduct",
            SortedAt = sortedAt ?? DateTime.Now
        };

    private static CellMeasurement CreateMeasurement(string cellId, double ocvVoltage, double irResistance)
        => new()
        {
            CellId = cellId,
            OcvVoltage = ocvVoltage,
            IrResistance = irResistance,
            TestStation = "TEST-STATION"
        };
}

public sealed class SortingLogRepositoryFixture : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly IFreeSql _fsql;

    public ISortingLogRepository Repository { get; }

    public SortingLogRepositoryFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sorting_test_{Guid.NewGuid():N}.db");
        _fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbPath};")
            .UseAutoSyncStructure(true)
            .Build();
        Repository = new SortingLogRepository(_fsql, NullLogger<SortingLogRepository>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _fsql.Dispose();
        await Task.Delay(100);
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}

// Total tests in this file: 17
// Coverage: Write / Query / Statistics / MesPending
