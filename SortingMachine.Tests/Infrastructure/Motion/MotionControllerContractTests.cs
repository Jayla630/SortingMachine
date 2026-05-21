// =========================================================
// File: SortingMachine.Tests/Infrastructure/Motion/MotionControllerContractTests.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Codex
// =========================================================

using FluentAssertions;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Tests.Helpers;
using Xunit;

namespace SortingMachine.Tests.Infrastructure.Motion;

public abstract class MotionControllerContractTests
{
    private static readonly AxisId[] AllAxes = new[] { AxisId.X, AxisId.Y, AxisId.Z };

    protected abstract IMotionController CreateController();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task InitializeAsync_WhenCalled_ShouldSucceed()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.InitializeAsync();

        // Assert
        result.Should().BeSuccessful();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task InitializeAsync_WhenCalledTwice_ShouldSucceed()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();

        // Act
        var result = await controller.InitializeAsync();

        // Assert
        result.Should().BeSuccessful();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task EnableAxisAsync_WhenInitialized_ShouldSucceed()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();

        // Act
        var result = await controller.EnableAxisAsync(AxisId.X);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        result.Should().BeSuccessful();
        status.Should().BeEnabled();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task EnableAxisAsync_WhenNotInitialized_ShouldMatchControllerContract()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.EnableAxisAsync(AxisId.X);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        result.Should().BeSuccessful();
        status.Should().BeEnabled();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task DisableAxisAsync_WhenAxisEnabled_ShouldDisableAxis()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();
        await controller.EnableAxisAsync(AxisId.X);

        // Act
        var result = await controller.DisableAxisAsync(AxisId.X);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        result.Should().BeSuccessful();
        status.Should().BeDisabled();
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task HomeAsync_WhenAxisEnabled_ShouldHomeAxis()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();
        await controller.EnableAxisAsync(AxisId.Z);

        // Act
        var result = await controller.HomeAsync(AxisId.Z);
        var status = await controller.GetAxisStatusAsync(AxisId.Z);

        // Assert
        result.Should().BeSuccessful();
        status.Should().BeHomed();
        status.Should().BeAtPosition(0, 0.001);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Contract")]
    public async Task HomeAllAxesAsync_WhenAllAxesEnabled_ShouldHomeAllAxes()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();
        await EnableAllAxesAsync(controller);

        // Act
        var result = await controller.HomeAllAxesAsync();
        var statuses = await GetAllAxisStatusesAsync(controller);

        // Assert
        result.Should().BeSuccessful();
        statuses.Should().OnlyContain(status => status.IsHomed);
        statuses.Should().OnlyContain(status => Math.Abs(status.Position) <= 0.001);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Contract")]
    public async Task HomeAllAxesAsync_WhenAllAxesEnabled_ShouldHomeZBeforeXAndY()
    {
        // Arrange
        var controller = CreateController();
        var homedOrder = new List<AxisId>();
        controller.AxisStatusChanged += (_, args) =>
        {
            if (args.CurrentStatus.IsHomed && !args.PreviousStatus.IsHomed)
            {
                homedOrder.Add(args.CurrentStatus.AxisId);
            }
        };
        await controller.InitializeAsync();
        await EnableAllAxesAsync(controller);

        // Act
        var result = await controller.HomeAllAxesAsync();

        // Assert
        result.Should().BeSuccessful();
        homedOrder.Should().StartWith(AxisId.Z);
        homedOrder.Should().ContainInOrder(AxisId.Z, AxisId.X, AxisId.Y);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task MoveAbsoluteAsync_WhenTargetWithinLimits_ShouldMoveToPosition()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);

        // Act
        var result = await controller.MoveAbsoluteAsync(AxisId.X, 100, 100);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        result.Should().BeSuccessful();
        status.Should().BeAtPosition(100, 0.001);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task MoveAbsoluteAsync_WhenExceedsPositiveLimit_ShouldFailWithLimitHit()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);

        // Act
        var result = await controller.MoveAbsoluteAsync(AxisId.X, 501, 100);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        result.Should().FailWith(MotionErrorCode.LimitHit);
        status.PositiveLimitHit.Should().BeTrue();
        status.Should().BeAtPosition(500, 0.001);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task MoveAbsoluteAsync_WhenExceedsNegativeLimit_ShouldFailWithLimitHit()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);

        // Act
        var result = await controller.MoveAbsoluteAsync(AxisId.X, -1, 100);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        result.Should().FailWith(MotionErrorCode.LimitHit);
        status.NegativeLimitHit.Should().BeTrue();
        status.Should().BeAtPosition(0, 0.001);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task MoveRelativeAsync_WhenCalledTwice_ShouldAccumulatePosition()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);

        // Act
        var firstResult = await controller.MoveRelativeAsync(AxisId.X, 20, 100);
        var secondResult = await controller.MoveRelativeAsync(AxisId.X, 30, 100);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        firstResult.Should().BeSuccessful();
        secondResult.Should().BeSuccessful();
        status.Should().BeAtPosition(50, 0.001);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task MoveAbsoluteAsync_WhenCancellationRequested_ShouldFailWithCancelled()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);
        using var cts = new CancellationTokenSource();

        // Act
        var moveTask = controller.MoveAbsoluteAsync(AxisId.X, 100, 10, cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();
        var result = await moveTask;

        // Assert
        result.Should().FailWith(MotionErrorCode.Cancelled);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task JogAsync_WhenStopped_ShouldSetMovingUntilStop()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);

        // Act
        var jogResult = await controller.JogAsync(AxisId.X, 25);
        var movingStatus = await controller.GetAxisStatusAsync(AxisId.X);
        var stopResult = await controller.StopAsync(AxisId.X);
        var stoppedStatus = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        jogResult.Should().BeSuccessful();
        movingStatus.Should().BeMoving();
        stopResult.Should().BeSuccessful();
        stoppedStatus.Should().BeStopped();
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task EmergencyStopAsync_WhenAxesAreMoving_ShouldStopAllAxes()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);
        await controller.JogAsync(AxisId.X, 25);

        // Act
        var result = await controller.EmergencyStopAsync();
        var statuses = await GetAllAxisStatusesAsync(controller);

        // Assert
        result.Should().BeSuccessful();
        statuses.Should().OnlyContain(status => !status.IsMoving);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task MoveAbsoluteAsync_WhenEmergencyStopActive_ShouldFailWithAlarmActive()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);
        await controller.EmergencyStopAsync();

        // Act
        var result = await controller.MoveAbsoluteAsync(AxisId.X, 10, 100);

        // Assert
        result.Should().FailWith(MotionErrorCode.AlarmActive);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Contract")]
    public async Task ClearAllAlarmsAsync_WhenEmergencyStopActive_ShouldAllowMotion()
    {
        // Arrange
        var controller = CreateController();
        await PrepareReadyAxisAsync(controller, AxisId.X);
        await controller.EmergencyStopAsync();

        // Act
        var clearResult = await controller.ClearAllAlarmsAsync();
        var moveResult = await controller.MoveAbsoluteAsync(AxisId.X, 10, 100);
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        clearResult.Should().BeSuccessful();
        moveResult.Should().BeSuccessful();
        status.Should().HaveNoAlarm();
        status.Should().BeAtPosition(10, 0.001);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task EnableAxisAsync_WhenStatusChanges_ShouldRaiseAxisStatusChanged()
    {
        // Arrange
        var controller = CreateController();
        AxisStatusChangedEventArgs? received = null;
        controller.AxisStatusChanged += (_, args) => received = args;
        await controller.InitializeAsync();

        // Act
        var result = await controller.EnableAxisAsync(AxisId.X);

        // Assert
        result.Should().BeSuccessful();
        received.Should().NotBeNull();
        received!.CurrentStatus.AxisId.Should().Be(AxisId.X);
        received.CurrentStatus.IsEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task EmergencyStopAsync_WhenCalled_ShouldRaiseAlarmOccurred()
    {
        // Arrange
        var controller = CreateController();
        var receivedAlarms = new List<MotionAlarmEventArgs>();
        controller.AlarmOccurred += (_, args) => receivedAlarms.Add(args);
        await PrepareReadyAxisAsync(controller, AxisId.X);

        // Act
        var result = await controller.EmergencyStopAsync();

        // Assert
        result.Should().BeSuccessful();
        receivedAlarms.Should().NotBeEmpty();
        receivedAlarms.Should().Contain(args => args.AxisId == AxisId.X);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Contract")]
    public async Task IsAllAxesReadyAsync_WhenAllAxesEnabledAndHomed_ShouldReturnTrue()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();
        await EnableAllAxesAsync(controller);
        await controller.HomeAllAxesAsync();

        // Act
        var isReady = await controller.IsAllAxesReadyAsync();

        // Assert
        isReady.Should().BeTrue();
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Contract")]
    public async Task IsAllAxesReadyAsync_WhenAnyAxisHasAlarm_ShouldReturnFalse()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();
        await EnableAllAxesAsync(controller);
        await controller.HomeAllAxesAsync();
        await controller.EmergencyStopAsync();

        // Act
        var isReady = await controller.IsAllAxesReadyAsync();

        // Assert
        isReady.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task IsAllAxesReadyAsync_WhenAnyAxisNotHomed_ShouldReturnFalse()
    {
        // Arrange
        var controller = CreateController();
        await controller.InitializeAsync();
        await EnableAllAxesAsync(controller);

        // Act
        var isReady = await controller.IsAllAxesReadyAsync();

        // Assert
        isReady.Should().BeFalse();
    }

    private static async Task PrepareReadyAxisAsync(IMotionController controller, AxisId axis)
    {
        await controller.InitializeAsync();
        await controller.EnableAxisAsync(axis);
        await controller.HomeAsync(axis);
    }

    private static async Task EnableAllAxesAsync(IMotionController controller)
    {
        foreach (var axis in AllAxes)
        {
            await controller.EnableAxisAsync(axis);
        }
    }

    private static async Task<IReadOnlyList<AxisStatus>> GetAllAxisStatusesAsync(IMotionController controller)
    {
        var statuses = new List<AxisStatus>();
        foreach (var axis in AllAxes)
        {
            statuses.Add(await controller.GetAxisStatusAsync(axis));
        }

        return statuses;
    }
}

// Total tests in this file: 22
// Coverage: Initialize / Enable / Home / Move / EStop / Event / Ready
