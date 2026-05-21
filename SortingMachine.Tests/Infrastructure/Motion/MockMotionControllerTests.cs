// =========================================================
// File: SortingMachine.Tests/Infrastructure/Motion/MockMotionControllerTests.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Codex
// =========================================================

using FluentAssertions;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Tests.Helpers;
using Xunit;

namespace SortingMachine.Tests.Infrastructure.Motion;

public sealed class MockMotionControllerTests : MotionControllerContractTests
{
    protected override IMotionController CreateController()
    {
        return new MockMotionController();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SimulateAlarm_WhenCalled_ShouldRaiseAlarmOccurredAndSetAxisAlarm()
    {
        // Arrange
        var controller = new MockMotionController();
        MotionAlarmEventArgs? received = null;
        controller.AlarmOccurred += (_, args) => received = args;
        await controller.InitializeAsync();

        // Act
        controller.SimulateAlarm(AxisId.X, "Unit test alarm");
        var status = await controller.GetAxisStatusAsync(AxisId.X);

        // Assert
        received.Should().NotBeNull();
        received!.AxisId.Should().Be(AxisId.X);
        received.AlarmMessage.Should().Be("Unit test alarm");
        status.Should().HaveAlarm();
        status.AlarmMessage.Should().Be("Unit test alarm");
    }
}

// Total tests in this file: 1
// Coverage: Mock-specific alarm simulation
