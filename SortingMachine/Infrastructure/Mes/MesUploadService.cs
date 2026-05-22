// =========================================================
// File: Infrastructure/Mes/MesUploadService.cs
// Project: SortingMachine
// Sprint: S5 | Agent: Claude Code
// =========================================================

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain;

namespace SortingMachine.Infrastructure.Mes;

public sealed class MesUploadService : IMesUploader
{
    private readonly HttpClient _httpClient;
    private readonly ISortingLogRepository _logRepository;
    private readonly ILogger<MesUploadService> _logger;
    private readonly string _endpoint;

    public string Endpoint => _endpoint;

    public MesUploadService(
        ISortingLogRepository logRepository,
        IConfiguration configuration,
        ILogger<MesUploadService> logger)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _logRepository = logRepository;
        _endpoint = configuration["Mes:Endpoint"] ?? "http://localhost:8080";
        _logger = logger;
    }

    public async Task<MesUploadResult> UploadPendingAsync(CancellationToken ct = default)
    {
        var pending = (await _logRepository.GetPendingMesUploadAsync()).ToList();
        if (!pending.Any())
            return MesUploadResult.Success(0);

        try
        {
            var payload = pending.Select(MapToDto);
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_endpoint}/api/sorting/batch", content, ct);

            if (response.IsSuccessStatusCode)
            {
                await _logRepository.MarkAsUploadedAsync(pending.Select(x => x.Id));
                _logger.LogInformation("MES upload success: {Count} records", pending.Count);
                return MesUploadResult.Success(pending.Count);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("MES upload failed: {Status} {Error}",
                response.StatusCode, error);
            return MesUploadResult.Failure($"HTTP {response.StatusCode}: {error}", pending.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MES upload exception");
            return MesUploadResult.Failure(ex.Message, pending.Count);
        }
    }

    public async Task<MesUploadResult> UploadSingleAsync(SortingLog log, CancellationToken ct = default)
    {
        try
        {
            var payload = MapToDto(log);
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_endpoint}/api/sorting/single", content, ct);

            if (response.IsSuccessStatusCode)
            {
                await _logRepository.MarkAsUploadedAsync(new[] { log.Id });
                _logger.LogInformation("MES single upload success: {CellId}", log.CellId);
                return MesUploadResult.Success(1);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("MES single upload failed: {Status} {Error}",
                response.StatusCode, error);
            return MesUploadResult.Failure($"HTTP {response.StatusCode}: {error}", 1);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MES single upload exception");
            return MesUploadResult.Failure(ex.Message, 1);
        }
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_endpoint}/api/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static object MapToDto(SortingLog log) => new
    {
        cellId = log.CellId,
        ocvVoltage = log.OcvVoltage,
        irResistance = log.IrResistance,
        grade = log.Grade,
        gradeReason = log.GradeReason,
        binId = log.BinId,
        isSuccess = log.IsSuccess,
        durationMs = log.DurationMs,
        recipeId = log.RecipeId,
        productModel = log.ProductModel,
        sortedAt = log.SortedAt.ToString("O")
    };
}
