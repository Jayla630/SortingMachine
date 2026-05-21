// =========================================================
// File: SortingMachine.Tests/Domain/GradingRulesTests.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Codex
// =========================================================

using FluentAssertions;
using SortingMachine.Domain.Recipe;
using Xunit;

namespace SortingMachine.Tests.Domain;

public sealed class GradingRulesTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenOcvBelowMinimum_ShouldReturnNgWithOcvMetric()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3199, 10);

        // Assert
        decision.Grade.Should().Be(CellGrade.NG);
        decision.TriggeringMetric.Should().Contain("OCV");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenOcvAboveMaximum_ShouldReturnNg()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(4251, 10);

        // Assert
        decision.Grade.Should().Be(CellGrade.NG);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenIrAboveMaximum_ShouldReturnNgWithIrMetric()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3800, 51);

        // Assert
        decision.Grade.Should().Be(CellGrade.NG);
        decision.TriggeringMetric.Should().Contain("IR");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenIrBelowMinimum_ShouldReturnNg()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3800, -0.001);

        // Assert
        decision.Grade.Should().Be(CellGrade.NG);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenTypicalAValues_ShouldReturnA()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3800, 15);

        // Assert
        decision.Grade.Should().Be(CellGrade.A);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenAtLowerABoundaries_ShouldReturnA()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(rules.OcvMin_A, rules.IrMax_A);

        // Assert
        decision.Grade.Should().Be(CellGrade.A);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenAtUpperAOcvBoundaryAndZeroIr_ShouldReturnA()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(rules.OcvMax_A, 0);

        // Assert
        decision.Grade.Should().Be(CellGrade.A);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenTypicalBValues_ShouldReturnB()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3500, 30);

        // Assert
        decision.Grade.Should().Be(CellGrade.B);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenAtLowerBOcvBoundary_ShouldReturnB()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(rules.OcvMin_B, 25);

        // Assert
        decision.Grade.Should().Be(CellGrade.B);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenIrExceedsAButWithinB_ShouldReturnB()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3800, 25);

        // Assert
        decision.Grade.Should().Be(CellGrade.B);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenOcvBelowBButQualified_ShouldReturnC()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3300, 10);

        // Assert
        decision.Grade.Should().Be(CellGrade.C);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenIrExceedsBButQualified_ShouldReturnC()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(3800, 40);

        // Assert
        decision.Grade.Should().Be(CellGrade.C);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenOcvJustBelowMinimum_ShouldReturnNg()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(rules.OcvMin - 0.001, 10);

        // Assert
        decision.Grade.Should().Be(CellGrade.NG);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenOcvAtMinimum_ShouldReturnQualifiedGrade()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var decision = rules.DetermineGrade(rules.OcvMin, 10);

        // Assert
        decision.Grade.Should().NotBe(CellGrade.NG);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DetermineGrade_WhenCalledRepeatedlyWithSameValues_ShouldReturnConsistentResult()
    {
        // Arrange
        var rules = new GradingRules();

        // Act
        var first = rules.DetermineGrade(3800, 15);
        var second = rules.DetermineGrade(3800, 15);

        // Assert
        second.Should().BeEquivalentTo(first);
    }
}

// Total tests in this file: 15
// Coverage: NG / AGrade / BGrade / CGrade / Boundary / Idempotency
