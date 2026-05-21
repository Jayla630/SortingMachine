// =========================================================
// File: SortingMachine.Tests/Helpers/MotionResultAssertions.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Codex
// =========================================================

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Tests.Helpers;

public static class MotionResultAssertionExtensions
{
    public static MotionResultAssertions Should(this MotionResult instance)
    {
        return new MotionResultAssertions(instance);
    }
}

public sealed class MotionResultAssertions : ReferenceTypeAssertions<MotionResult, MotionResultAssertions>
{
    public MotionResultAssertions(MotionResult subject)
        : base(subject)
    {
    }

    protected override string Identifier => "motion result";

    public AndConstraint<MotionResultAssertions> BeSuccessful(string because = "", params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:motion result} to be successful{reason}, but found <null>.")
            .Then
            .ForCondition(Subject!.IsSuccess)
            .FailWith(
                "Expected {context:motion result} to be successful{reason}, but it failed with {0}: {1}.",
                Subject.ErrorCode,
                Subject.ErrorMessage);

        return new AndConstraint<MotionResultAssertions>(this);
    }

    public AndConstraint<MotionResultAssertions> FailWith(
        MotionErrorCode expectedErrorCode,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:motion result} to fail with {0}{reason}, but found <null>.", expectedErrorCode)
            .Then
            .ForCondition(!Subject!.IsSuccess)
            .FailWith("Expected {context:motion result} to fail with {0}{reason}, but it succeeded.", expectedErrorCode)
            .Then
            .ForCondition(Subject.ErrorCode == expectedErrorCode)
            .FailWith(
                "Expected {context:motion result} to fail with {0}{reason}, but found {1}.",
                expectedErrorCode,
                Subject.ErrorCode);

        return new AndConstraint<MotionResultAssertions>(this);
    }
}

