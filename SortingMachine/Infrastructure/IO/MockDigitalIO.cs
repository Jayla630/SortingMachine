// =========================================================
// File: Infrastructure/IO/MockDigitalIO.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
namespace SortingMachine.Infrastructure.IO;

public class MockDigitalIO : IDigitalIO
{
    private readonly Dictionary<int, bool> _inputs = new();
    private readonly Dictionary<int, bool> _outputs = new();

    public Task<bool> ReadInputAsync(int channel)
    {
        _inputs.TryGetValue(channel, out var val);
        return Task.FromResult(val);
    }

    public Task WriteOutputAsync(int channel, bool value)
    {
        _outputs[channel] = value;
        return Task.CompletedTask;
    }
}
