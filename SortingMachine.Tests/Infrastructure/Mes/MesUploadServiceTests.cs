// =========================================================
// File: SortingMachine.Tests/Infrastructure/Mes/MesUploadServiceTests.cs
// Project: SortingMachine
// Sprint: S5 | Agent: Codex
// =========================================================

using FluentAssertions;
using FreeSql;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SortingMachine.Domain;
using SortingMachine.Infrastructure.Mes;
using SortingMachine.Infrastructure.Persistence;
using Xunit;

namespace SortingMachine.Tests.Infrastructure.Mes;

public sealed class MesUploadServiceTests
{
    [Fact(Timeout = 3000)]
    [Trait("Category", "Unit")]
    public async Task UploadPendingAsync_WhenNoPendingLogs_ShouldReturnSuccessWithZeroCount()
    {
        // Arrange
        var repository = new Mock<ISortingLogRepository>();
        repository.Setup(r => r.GetPendingMesUploadAsync()).ReturnsAsync(Array.Empty<SortingLog>());
        var uploader = CreateUploader(repository.Object);

        // Act
        var result = await uploader.UploadPendingAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        repository.Verify(r => r.MarkAsUploadedAsync(It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact(Timeout = 3000)]
    [Trait("Category", "Unit")]
    public async Task UploadPendingAsync_WhenThreePendingLogs_ShouldReturnSuccessWithThreeCount()
    {
        // Arrange
        var logs = CreateLogs(3);
        var repository = CreatePendingRepository(logs);
        var uploader = CreateUploader(repository.Object);

        // Act
        var result = await uploader.UploadPendingAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(3);
        result.FailedCount.Should().Be(0);
    }

    [Fact(Timeout = 3000)]
    [Trait("Category", "Unit")]
    public async Task UploadPendingAsync_WhenUploadSucceeds_ShouldMarkExpectedIdsAsUploaded()
    {
        // Arrange
        var logs = CreateLogs(3);
        var expectedIds = logs.Select(log => log.Id).ToArray();
        var repository = CreatePendingRepository(logs);
        var uploader = CreateUploader(repository.Object);

        // Act
        var result = await uploader.UploadPendingAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        repository.Verify(
            r => r.MarkAsUploadedAsync(It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(expectedIds))),
            Times.Once);
    }

    [Fact(Timeout = 6000)]
    [Trait("Category", "Integration")]
    public async Task UploadPendingAsync_WhenUploadSucceeds_ShouldLeaveNoPendingLogs()
    {
        // Arrange
        await using var fixture = new MesUploadTestFixture();
        await fixture.SeedLogsAsync(3);

        // Act
        var result = await fixture.MesUploader.UploadPendingAsync();
        var pending = await fixture.LogRepository.GetPendingMesUploadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(3);
        pending.Should().BeEmpty();
    }

    [Fact(Timeout = 3000)]
    [Trait("Category", "Unit")]
    public async Task UploadPendingAsync_WhenCancellationRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var repository = CreatePendingRepository(CreateLogs(1));
        var uploader = CreateUploader(repository.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var uploadTask = uploader.UploadPendingAsync(cts.Token);
        await cts.CancelAsync();
        var act = async () => await uploadTask;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        repository.Verify(r => r.MarkAsUploadedAsync(It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact(Timeout = 3000)]
    [Trait("Category", "Unit")]
    public async Task UploadSingleAsync_WhenLogProvided_ShouldReturnSuccessWithOneCount()
    {
        // Arrange
        var repository = new Mock<ISortingLogRepository>();
        repository.Setup(r => r.MarkAsUploadedAsync(It.IsAny<IEnumerable<long>>())).Returns(Task.CompletedTask);
        var uploader = CreateUploader(repository.Object);
        var log = CreateLog(42);

        // Act
        var result = await uploader.UploadSingleAsync(log);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
    }

    [Fact(Timeout = 3000)]
    [Trait("Category", "Unit")]
    public async Task UploadSingleAsync_WhenLogProvided_ShouldMarkOnlyThatIdOnce()
    {
        // Arrange
        var repository = new Mock<ISortingLogRepository>();
        repository.Setup(r => r.MarkAsUploadedAsync(It.IsAny<IEnumerable<long>>())).Returns(Task.CompletedTask);
        var uploader = CreateUploader(repository.Object);
        var log = CreateLog(42);

        // Act
        var result = await uploader.UploadSingleAsync(log);

        // Assert
        result.IsSuccess.Should().BeTrue();
        repository.Verify(
            r => r.MarkAsUploadedAsync(It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(new[] { 42L }))),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Success_WhenCountProvided_ShouldCreateSuccessfulResult()
    {
        // Arrange

        // Act
        var result = MesUploadResult.Success(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(5);
        result.FailedCount.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Failure_WhenErrorAndFailedCountProvided_ShouldCreateFailedResult()
    {
        // Arrange

        // Act
        var result = MesUploadResult.Failure("error", 3);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(3);
        result.ErrorMessage.Should().Be("error");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Success_WhenCreated_ShouldSetUploadedAtWithinReasonableTimeRange()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);

        // Act
        var result = MesUploadResult.Success(1);
        var after = DateTime.Now.AddSeconds(1);

        // Assert
        result.UploadedAt.Should().BeOnOrAfter(before);
        result.UploadedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PingAsync_WhenUsingMockMesUploadService_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var uploader = CreateUploader(Mock.Of<ISortingLogRepository>());

        // Act
        var result = await uploader.PingAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Timeout = 6000)]
    [Trait("Category", "Integration")]
    public async Task GetPendingMesUploadAsync_WhenThreeSuccessfulLogsWritten_ShouldReturnThreeLogs()
    {
        // Arrange
        await using var fixture = new MesUploadTestFixture();

        // Act
        await fixture.SeedLogsAsync(3);
        var pending = (await fixture.LogRepository.GetPendingMesUploadAsync()).ToList();

        // Assert
        pending.Should().HaveCount(3);
        pending.Should().OnlyContain(log => log.IsSuccess);
    }

    [Fact(Timeout = 6000)]
    [Trait("Category", "Integration")]
    public async Task UploadPendingAsync_WhenUsingRealRepository_ShouldMarkAllLogsAsUploaded()
    {
        // Arrange
        await using var fixture = new MesUploadTestFixture();
        await fixture.SeedLogsAsync(3);

        // Act
        var result = await fixture.MesUploader.UploadPendingAsync();
        var pending = await fixture.LogRepository.GetPendingMesUploadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(3);
        pending.Should().BeEmpty();
    }

    [Fact(Timeout = 6000)]
    [Trait("Category", "Integration")]
    public async Task UploadPendingAsync_WhenUsingRealRepository_ShouldKeepOriginalRecords()
    {
        // Arrange
        await using var fixture = new MesUploadTestFixture();
        await fixture.SeedLogsAsync(3);

        // Act
        var result = await fixture.MesUploader.UploadPendingAsync();
        var totalCount = await fixture.LogRepository.GetTotalCountAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        totalCount.Should().Be(3);
    }

    private static MockMesUploadService CreateUploader(ISortingLogRepository repository)
        => new(repository, NullLogger<MockMesUploadService>.Instance);

    private static Mock<ISortingLogRepository> CreatePendingRepository(IReadOnlyCollection<SortingLog> logs)
    {
        var repository = new Mock<ISortingLogRepository>();
        repository.Setup(r => r.GetPendingMesUploadAsync()).ReturnsAsync(logs);
        repository.Setup(r => r.MarkAsUploadedAsync(It.IsAny<IEnumerable<long>>())).Returns(Task.CompletedTask);
        return repository;
    }

    private static IReadOnlyList<SortingLog> CreateLogs(int count)
        => Enumerable.Range(1, count)
            .Select(i => CreateLog(i))
            .ToList();

    private static SortingLog CreateLog(long id, bool isSuccess = true)
        => new()
        {
            Id = id,
            CellId = $"CELL-{id:000}",
            OcvVoltage = 3800,
            IrResistance = 15,
            Grade = "A",
            GradeReason = "A级品",
            BinId = isSuccess ? "BIN-A" : null,
            IsSuccess = isSuccess,
            DurationMs = 1200,
            SortedAt = DateTime.Now
        };
}

public sealed class MesUploadTestFixture : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly IFreeSql _fsql;

    public ISortingLogRepository LogRepository { get; }
    public IMesUploader MesUploader { get; }

    public MesUploadTestFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mes_test_{Guid.NewGuid():N}.db");
        _fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbPath};")
            .UseAutoSyncStructure(true)
            .Build();

        LogRepository = new SortingLogRepository(_fsql, NullLogger<SortingLogRepository>.Instance);
        MesUploader = new MockMesUploadService(LogRepository, NullLogger<MockMesUploadService>.Instance);
    }

    public async Task SeedLogsAsync(int count, bool isSuccess = true)
    {
        for (var i = 0; i < count; i++)
        {
            await LogRepository.SaveAsync(new SortingLog
            {
                CellId = $"CELL-{i:000}",
                OcvVoltage = 3800,
                IrResistance = 15,
                Grade = "A",
                GradeReason = "A级品",
                BinId = isSuccess ? "BIN-A" : null,
                IsSuccess = isSuccess,
                DurationMs = 1200,
                SortedAt = DateTime.Now
            });
        }

        await Task.Delay(100);
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

// Total tests in this file: 14
// Coverage: UploadPending / UploadSingle / MarkAsUploaded / Result / Integration
