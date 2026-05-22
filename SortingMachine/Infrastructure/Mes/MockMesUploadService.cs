// =========================================================
// File: Infrastructure/Mes/MockMesUploadService.cs
// Project: SortingMachine
// Sprint: S5 | Agent: Claude Code
// =========================================================

using Microsoft.Extensions.Logging;
using SortingMachine.Domain;

namespace SortingMachine.Infrastructure.Mes;

public sealed class MockMesUploadService : IMesUploader
{
    private readonly ISortingLogRepository _logRepository;
    private readonly ILogger<MockMesUploadService> _logger;

    public string Endpoint => "Mock（无需 MES 服务器）";

    public MockMesUploadService(
        ISortingLogRepository logRepository,
        ILogger<MockMesUploadService> logger)
    {
        _logRepository = logRepository;
        _logger = logger;
    }

    public async Task<MesUploadResult> UploadPendingAsync(CancellationToken ct = default)
    {
        var pending = (await _logRepository.GetPendingMesUploadAsync()).ToList();
        if (!pending.Any())
            return MesUploadResult.Success(0);

        // 模拟网络延迟
        await Task.Delay(500, ct);

        // 标记为已上报
        await _logRepository.MarkAsUploadedAsync(pending.Select(x => x.Id));

        _logger.LogInformation("[Mock MES] Uploaded {Count} records", pending.Count);
        return MesUploadResult.Success(pending.Count);
    }

    public async Task<MesUploadResult> UploadSingleAsync(SortingLog log, CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        await _logRepository.MarkAsUploadedAsync(new[] { log.Id });
        _logger.LogInformation("[Mock MES] Uploaded single record: {CellId}", log.CellId);
        return MesUploadResult.Success(1);
    }

    public Task<bool> PingAsync(CancellationToken ct = default)
        => Task.FromResult(true); // Mock 永远在线
}
