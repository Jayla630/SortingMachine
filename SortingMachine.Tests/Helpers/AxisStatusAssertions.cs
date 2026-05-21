// =========================================================
// File: SortingMachine.Tests/Helpers/AxisStatusAssertions.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Codex
// =========================================================

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Tests.Helpers;

public static class AxisStatusAssertionExtensions
{
    public static AxisStatusAssertions Should(this AxisStatus instance)
    {
        return new AxisStatusAssertions(instance);
    }
}

public sealed class AxisStatusAssertions : ReferenceTypeAssertions<AxisStatus, AxisStatusAssertions>
{
    public AxisStatusAssertions(AxisStatus subject)
        : base(subject)
    {
    }

    protected override string Identifier => "axis status";

    public AndConstraint<AxisStatusAssertions> BeEnabled(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to be enabled{reason}, but found <null>.")
            .Then
            .ForCondition(Subject!.IsEnabled)
            .FailWith("Expected {context:axis status} to be enabled{reason}, but it was disabled.");

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> BeDisabled(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to be disabled{reason}, but found <null>.")
            .Then
            .ForCondition(!Subject!.IsEnabled)
            .FailWith("Expected {context:axis status} to be disabled{reason}, but it was enabled.");

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> BeHomed(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to be homed{reason}, but found <null>.")
            .Then
            .ForCondition(Subject!.IsHomed)
            .FailWith("Expected {context:axis status} to be homed{reason}, but it was not homed.");

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> BeAtPosition(
        double expectedPosition,
        double tolerance,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to be at position {0}{reason}, but found <null>.", expectedPosition)
            .Then
            .ForCondition(Math.Abs(Subject!.Position - expectedPosition) <= tolerance)
            .FailWith(
                "Expected {context:axis status} to be at position {0} +/- {1}{reason}, but found {2}.",
                expectedPosition,
                tolerance,
                Subject.Position);

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> BeMoving(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to be moving{reason}, but found <null>.")
            .Then
            .ForCondition(Subject!.IsMoving)
            .FailWith("Expected {context:axis status} to be moving{reason}, but it was stopped.");

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> BeStopped(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to be stopped{reason}, but found <null>.")
            .Then
            .ForCondition(!Subject!.IsMoving)
            .FailWith("Expected {context:axis status} to be stopped{reason}, but it was moving.");

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> HaveNoAlarm(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to have no alarm{reason}, but found <null>.")
            .Then
            .ForCondition(!Subject!.HasAlarm)
            .FailWith("Expected {context:axis status} to have no alarm{reason}, but found {0}.", Subject.AlarmMessage);

        return new AndConstraint<AxisStatusAssertions>(this);
    }

    public AndConstraint<AxisStatusAssertions> HaveAlarm(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:axis status} to have an alarm{reason}, but found <null>.")
            .Then
            .ForCondition(Subject!.HasAlarm)
            .FailWith("Expected {context:axis status} to have an alarm{reason}, but no alarm was active.");

        return new AndConstraint<AxisStatusAssertions>(this);
    }
}

