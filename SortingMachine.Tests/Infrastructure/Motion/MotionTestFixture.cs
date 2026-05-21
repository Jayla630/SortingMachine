// =========================================================
// File: SortingMachine.Tests/Infrastructure/Motion/MotionTestFixture.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Codex
// =========================================================

using SortingMachine.Infrastructure.Motion;
using Xunit;

namespace SortingMachine.Tests.Infrastructure.Motion;

public sealed class MotionTestFixture : IAsyncLifetime
{
    public IMotionController Controller { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Controller = new MockMotionController();
        await Controller.InitializeAsync();

        foreach (var axis in AllAxes)
        {
            await Controller.EnableAxisAsync(axis);
        }

        await Controller.HomeAllAxesAsync();
    }

    public async Task DisposeAsync()
    {
        if (Controller is not null)
        {
            await Controller.DisconnectAsync();
        }
    }

    private static AxisId[] AllAxes => new[] { AxisId.X, AxisId.Y, AxisId.Z };
}
