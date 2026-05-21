// =========================================================
// File: Infrastructure/IO/IDigitalIO.cs
// Project: SortingMachine
// Sprint: S1 | Agent: Gemini CLI
// =========================================================
namespace SortingMachine.Infrastructure.IO;

/// <summary>
/// 数字量 IO 契约 (DI/DO 抽象)
/// TODO: Sprint S3 接入板卡 SDK 实现
/// </summary>
public interface IDigitalIO
{
    Task<bool> ReadInputAsync(int channel);
    Task WriteOutputAsync(int channel, bool value);
}
