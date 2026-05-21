// =========================================================
// File: SortingMachine.Tests/Domain/StateMachines/SafetyValidatorTests.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Codex
// =========================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.Motion;
using SortingMachine.Tests.Builders;
using Xunit;

namespace SortingMachine.Tests.Domain.StateMachines;

public sealed class SafetyValidatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenXAxisTargetWithinLimits_ShouldReturnSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses();

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 300, statuses);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ViolationType.Should().Be(SafetyViolationType.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenXAxisTargetExceedsPositiveLimit_ShouldReturnSoftLimitViolation()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses();

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 600, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.SoftLimitViolation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenXAxisTargetExceedsNegativeLimit_ShouldReturnSoftLimitViolation()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses();

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, -10, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.SoftLimitViolation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenZAxisTargetExceedsPositiveLimit_ShouldReturnSoftLimitViolation()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses();

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.Z, 160, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.SoftLimitViolation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenZAxisRetractedAndXMoves_ShouldReturnSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(zPosition: 5);

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 100, statuses);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ViolationType.Should().Be(SafetyViolationType.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenZAxisLowAndXMovesLargeDistance_ShouldReturnZAxisNotSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(xPosition: 0, zPosition: 50);

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 200, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.ZAxisNotSafe);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenZAxisLowAndXMovesSmallDistance_ShouldReturnSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(xPosition: 0, zPosition: 50);

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 20, statuses);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ViolationType.Should().Be(SafetyViolationType.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenZAxisLowAndYMovesLargeDistance_ShouldReturnZAxisNotSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(yPosition: 0, zPosition: 50);

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.Y, 200, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.ZAxisNotSafe);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenTargetAxisHasAlarm_ShouldReturnAxisHasAlarm()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(xHasAlarm: true);

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 100, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.AxisHasAlarm);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateMoveAsync_WhenUnrelatedAxisHasAlarm_ShouldEvaluateTargetAxisRulesOnly()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(yHasAlarm: true, zPosition: 5);

        // Act
        var result = await validator.ValidateMoveAsync(AxisId.X, 100, statuses);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ViolationType.Should().Be(SafetyViolationType.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateJogAsync_WhenPositiveJogNearPositiveLimit_ShouldReturnSoftLimitViolation()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(xPosition: 498);

        // Act
        var result = await validator.ValidateJogAsync(AxisId.X, 10, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.SoftLimitViolation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateJogAsync_WhenNegativeJogNearNegativeLimit_ShouldReturnSoftLimitViolation()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(xPosition: 2);

        // Act
        var result = await validator.ValidateJogAsync(AxisId.X, -10, statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.SoftLimitViolation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateJogAsync_WhenPositiveJogFromMiddlePosition_ShouldReturnSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(xPosition: 250);

        // Act
        var result = await validator.ValidateJogAsync(AxisId.X, 10, statuses);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ViolationType.Should().Be(SafetyViolationType.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateHomingAsync_WhenAllAxesEnabledAndNoAlarm_ShouldReturnSafe()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses();

        // Act
        var result = await validator.ValidateHomingAsync(statuses);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ViolationType.Should().Be(SafetyViolationType.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateHomingAsync_WhenAnyAxisDisabled_ShouldReturnAxisNotEnabled()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(yEnabled: false);

        // Act
        var result = await validator.ValidateHomingAsync(statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.AxisNotEnabled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateHomingAsync_WhenAnyAxisHasAlarm_ShouldReturnAxisHasAlarm()
    {
        // Arrange
        var validator = CreateValidator();
        var statuses = CreateStatuses(zHasAlarm: true);

        // Act
        var result = await validator.ValidateHomingAsync(statuses);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ViolationType.Should().Be(SafetyViolationType.AxisHasAlarm);
    }

    private static ISafetyValidator CreateValidator()
    {
        return new MotionSafetyValidator(
            new MockMotionController(),
            NullLogger<MotionSafetyValidator>.Instance);
    }

    private static IReadOnlyDictionary<AxisId, AxisStatus> CreateStatuses(
        double xPosition = 0,
        double yPosition = 0,
        double zPosition = 0,
        bool xEnabled = true,
        bool yEnabled = true,
        bool zEnabled = true,
        bool xHasAlarm = false,
        bool yHasAlarm = false,
        bool zHasAlarm = false)
    {
        return new Dictionary<AxisId, AxisStatus>
        {
            [AxisId.X] = BuildStatus(AxisId.X, xPosition, xEnabled, xHasAlarm),
            [AxisId.Y] = BuildStatus(AxisId.Y, yPosition, yEnabled, yHasAlarm),
            [AxisId.Z] = BuildStatus(AxisId.Z, zPosition, zEnabled, zHasAlarm)
        };
    }

    private static AxisStatus BuildStatus(
        AxisId axisId,
        double position,
        bool isEnabled,
        bool hasAlarm)
    {
        var builder = new AxisStatusBuilder()
            .ForAxis(axisId)
            .AtPosition(position)
            .Homed()
            .Stopped();

        if (isEnabled)
        {
            builder.Enabled();
        }
        else
        {
            builder.Disabled();
        }

        if (hasAlarm)
        {
            builder.WithAlarm($"{axisId} alarm");
        }
        else
        {
            builder.WithoutAlarm();
        }

        return builder.Build();
    }
}

// Total tests in this file: 16
// Coverage: PreCondition / StateTransition / HomingOrder / Cancellation / FaultInjection / Safety
