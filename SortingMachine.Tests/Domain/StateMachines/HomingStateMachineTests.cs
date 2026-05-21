// =========================================================
// File: SortingMachine.Tests/Domain/StateMachines/HomingStateMachineTests.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Codex
// =========================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.Motion;
using Xunit;

namespace SortingMachine.Tests.Domain.StateMachines;

public sealed class HomingStateMachineTests
{
    private static readonly AxisId[] AllAxes = new[] { AxisId.X, AxisId.Y, AxisId.Z };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CurrentState_WhenCreated_ShouldBeIdle()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();

        // Act
        var state = fixture.StateMachine.CurrentState;

        // Assert
        state.Should().Be(HomingState.Idle);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task Reset_WhenStateWasCompleted_ShouldReturnToIdle()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        await fixture.StateMachine.ExecuteAsync();

        // Act
        fixture.StateMachine.Reset();

        // Assert
        fixture.StateMachine.CurrentState.Should().Be(HomingState.Idle);
    }

    [Fact(Timeout = 8000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenAllAxesEnabledAndNoAlarm_ShouldEnterHomingZ()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);

        // Act
        var executeTask = fixture.StateMachine.ExecuteAsync();
        await WaitUntilAsync(() => changes.Any(x => x.CurrentState == HomingState.HomingZ));
        await fixture.StateMachine.CancelAsync();
        await executeTask;

        // Assert
        changes.Select(x => x.CurrentState).Should().Contain(HomingState.HomingZ);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenAnyAxisDisabled_ShouldReturnPreConditionFailed()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.MockController.EnableAxisAsync(AxisId.X);
        await fixture.MockController.EnableAxisAsync(AxisId.Z);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalState.Should().Be(HomingState.Failed);
        result.FailureReason.Should().Be(HomingFailureReason.PreConditionFailed);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenAnyAxisHasAlarm_ShouldReturnPreConditionFailed()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        fixture.MockController.SimulateAlarm(AxisId.Y, "Pre-check alarm");

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalState.Should().Be(HomingState.Failed);
        result.FailureReason.Should().Be(HomingFailureReason.PreConditionFailed);
    }

    [Fact(Timeout = 5000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenControllerNotInitialized_ShouldReturnFailure()
    {
        // Arrange
        var controller = new MockMotionController();
        var validator = CreateSafetyValidator(controller);
        var stateMachine = CreateStateMachine(controller, validator);

        // Act
        var result = await stateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalState.Should().Be(HomingState.Failed);
        result.FailureReason.Should().Be(HomingFailureReason.PreConditionFailed);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFullSequenceCompletes_ShouldReturnCompletedSuccess()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FinalState.Should().Be(HomingState.Completed);
        result.FailureReason.Should().Be(HomingFailureReason.None);
        fixture.StateMachine.CurrentState.Should().Be(HomingState.Completed);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFullSequenceCompletes_ShouldSetPositiveDuration()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenAlreadyCompleted_ShouldReturnSuccessWithoutRepeatingHoming()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        await fixture.StateMachine.ExecuteAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FinalState.Should().Be(HomingState.Completed);
        changes.Should().BeEmpty();
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFullSequenceCompletes_ShouldRaiseExpectedStateSequence()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Select(x => x.CurrentState).Should().ContainInOrder(
            HomingState.CheckingPreConditions,
            HomingState.HomingZ,
            HomingState.HomingX,
            HomingState.HomingY,
            HomingState.Completed);
        changes.First().PreviousState.Should().Be(HomingState.Idle);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFullSequenceCompletes_ShouldHomeZBeforeX()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureTimedStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Single(x => x.Args.CurrentState == HomingState.HomingX).Timestamp
            .Should().BeAfter(changes.Single(x => x.Args.CurrentState == HomingState.HomingZ).Timestamp);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFullSequenceCompletes_ShouldHomeXBeforeY()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureTimedStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Single(x => x.Args.CurrentState == HomingState.HomingY).Timestamp
            .Should().BeAfter(changes.Single(x => x.Args.CurrentState == HomingState.HomingX).Timestamp);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFullSequenceCompletes_ShouldHomeYLast()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Where(x => x.CurrentState is HomingState.HomingX or HomingState.HomingY or HomingState.Completed)
            .Select(x => x.CurrentState)
            .Should().ContainInOrder(HomingState.HomingX, HomingState.HomingY, HomingState.Completed);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task CancelAsync_WhenCalledDuringHomingZ_ShouldReturnCancelled()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);
        var executeTask = fixture.StateMachine.ExecuteAsync();
        await WaitUntilAsync(() => changes.Any(x => x.CurrentState == HomingState.HomingZ));

        // Act
        await fixture.StateMachine.CancelAsync();
        var result = await executeTask;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalState.Should().Be(HomingState.Cancelled);
        result.FailureReason.Should().Be(HomingFailureReason.Cancelled);
        fixture.StateMachine.CurrentState.Should().Be(HomingState.Cancelled);
    }

    [Fact(Timeout = 10000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenExternalCancellationTokenIsCancelled_ShouldReturnCancelled()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        using var cts = new CancellationTokenSource();
        var changes = CaptureStateChanges(fixture.StateMachine);
        var executeTask = fixture.StateMachine.ExecuteAsync(cts.Token);
        await WaitUntilAsync(() => changes.Any(x => x.CurrentState == HomingState.HomingZ));

        // Act
        await cts.CancelAsync();
        var result = await executeTask;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalState.Should().Be(HomingState.Cancelled);
        result.FailureReason.Should().Be(HomingFailureReason.Cancelled);
    }

    [Fact(Timeout = 20000)]
    [Trait("Category", "Unit")]
    public async Task Reset_WhenCancelled_ShouldReturnToIdleAndAllowRetry()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);
        var executeTask = fixture.StateMachine.ExecuteAsync();
        await WaitUntilAsync(() => changes.Any(x => x.CurrentState == HomingState.HomingZ));
        await fixture.StateMachine.CancelAsync();
        var cancelled = await executeTask;

        // Act
        fixture.StateMachine.Reset();
        var stateAfterReset = fixture.StateMachine.CurrentState;
        var retry = await fixture.StateMachine.ExecuteAsync();

        // Assert
        cancelled.FinalState.Should().Be(HomingState.Cancelled);
        stateAfterReset.Should().Be(HomingState.Idle);
        fixture.StateMachine.CurrentState.Should().Be(HomingState.Completed);
        retry.IsSuccess.Should().BeTrue();
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenZAxisAlarmOccursDuringHoming_ShouldReturnAxisAlarmDuringHoming()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);
        var executeTask = fixture.StateMachine.ExecuteAsync();
        await WaitUntilAsync(() => changes.Any(x => x.CurrentState == HomingState.HomingZ));

        // Act
        fixture.MockController.SimulateAlarm(AxisId.Z, "Z alarm during homing");
        var result = await executeTask;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalState.Should().Be(HomingState.Failed);
        result.FailureReason.Should().Be(HomingFailureReason.AxisAlarmDuringHoming);
    }

    [Fact(Timeout = 25000)]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_WhenFaultRecoveredWithResetAndClearAlarm_ShouldAllowRetry()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);
        var executeTask = fixture.StateMachine.ExecuteAsync();
        await WaitUntilAsync(() => changes.Any(x => x.CurrentState == HomingState.HomingZ));
        fixture.MockController.SimulateAlarm(AxisId.X, "Injected X alarm");
        var failed = await executeTask;

        // Act
        fixture.StateMachine.Reset();
        await fixture.MockController.ClearAllAlarmsAsync();
        var retry = await fixture.StateMachine.ExecuteAsync();

        // Assert
        failed.IsSuccess.Should().BeFalse();
        failed.FailureReason.Should().Be(HomingFailureReason.AxisAlarmDuringHoming);
        retry.IsSuccess.Should().BeTrue();
        retry.FinalState.Should().Be(HomingState.Completed);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task StateChanged_WhenExecuteAsyncRuns_ShouldRaiseAtLeastFiveTimes()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task StateChanged_WhenExecuteAsyncRuns_ShouldProvideLegalTransitions()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);
        var legalTransitions = new HashSet<(HomingState Previous, HomingState Current)>
        {
            (HomingState.Idle, HomingState.CheckingPreConditions),
            (HomingState.CheckingPreConditions, HomingState.HomingZ),
            (HomingState.CheckingPreConditions, HomingState.Failed),
            (HomingState.HomingZ, HomingState.HomingX),
            (HomingState.HomingZ, HomingState.Failed),
            (HomingState.HomingZ, HomingState.Cancelled),
            (HomingState.HomingX, HomingState.HomingY),
            (HomingState.HomingX, HomingState.Failed),
            (HomingState.HomingX, HomingState.Cancelled),
            (HomingState.HomingY, HomingState.Completed),
            (HomingState.HomingY, HomingState.Failed),
            (HomingState.HomingY, HomingState.Cancelled),
            (HomingState.Failed, HomingState.Idle),
            (HomingState.Completed, HomingState.Idle),
            (HomingState.Cancelled, HomingState.Idle)
        };

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Select(x => (x.PreviousState, x.CurrentState))
            .Should().OnlyContain(transition => legalTransitions.Contains(transition));
    }

    [Fact(Timeout = 15000)]
    [Trait("Category", "Unit")]
    public async Task StateChanged_WhenAlreadyCompletedExecuteIsCalled_ShouldNotRaiseAgain()
    {
        // Arrange
        await using var fixture = await HomingTestFixture.CreateAsync();
        await fixture.EnableAllAxesAsync();
        await fixture.StateMachine.ExecuteAsync();
        var changes = CaptureStateChanges(fixture.StateMachine);

        // Act
        var result = await fixture.StateMachine.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        changes.Should().BeEmpty();
    }

    private static ISafetyValidator CreateSafetyValidator(IMotionController controller)
    {
        return new MotionSafetyValidator(
            controller,
            NullLogger<MotionSafetyValidator>.Instance);
    }

    private static IHomingStateMachine CreateStateMachine(
        IMotionController controller,
        ISafetyValidator safetyValidator)
    {
        return new HomingStateMachine(
            controller,
            safetyValidator,
            NullLogger<HomingStateMachine>.Instance);
    }

    private static List<HomingStateChangedEventArgs> CaptureStateChanges(IHomingStateMachine stateMachine)
    {
        var changes = new List<HomingStateChangedEventArgs>();
        stateMachine.StateChanged += (_, args) => changes.Add(args);
        return changes;
    }

    private static List<TimedStateChange> CaptureTimedStateChanges(IHomingStateMachine stateMachine)
    {
        var changes = new List<TimedStateChange>();
        stateMachine.StateChanged += (_, args) => changes.Add(new TimedStateChange(DateTime.UtcNow, args));
        return changes;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline);
            await Task.Delay(25);
        }
    }

    private sealed record TimedStateChange(DateTime Timestamp, HomingStateChangedEventArgs Args);

    private sealed class HomingTestFixture : IAsyncDisposable
    {
        private HomingTestFixture(MockMotionController mockController)
        {
            MockController = mockController;
            SafetyValidator = CreateSafetyValidator(mockController);
            StateMachine = CreateStateMachine(mockController, SafetyValidator);
        }

        public MockMotionController MockController { get; }
        public IHomingStateMachine StateMachine { get; }
        public ISafetyValidator SafetyValidator { get; }

        public static async Task<HomingTestFixture> CreateAsync()
        {
            var controller = new MockMotionController();
            await controller.InitializeAsync();
            return new HomingTestFixture(controller);
        }

        public async Task EnableAllAxesAsync()
        {
            foreach (var axis in AllAxes)
            {
                await MockController.EnableAxisAsync(axis);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await MockController.DisconnectAsync();
        }
    }
}

// Total tests in this file: 21
// Coverage: PreCondition / StateTransition / HomingOrder / Cancellation / FaultInjection / Safety
