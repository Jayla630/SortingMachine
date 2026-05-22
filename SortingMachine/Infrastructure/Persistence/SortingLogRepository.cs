// =========================================================
// File: Infrastructure/Persistence/SortingLogRepository.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Claude Code
// =========================================================

using FreeSql;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain;

namespace SortingMachine.Infrastructure.Persistence;

/// <summary>
/// FreeSql 实体（仅用于 ORM 映射，不对外暴露）
/// </summary>
[Table(Name = "sorting_logs")]
internal class SortingLogEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public long Id { get; set; }

    [Column(StringLength = 100)]
    public string CellId { get; set; } = string.Empty;

    public double OcvVoltage { get; set; }
    public double IrResistance { get; set; }

    [Column(StringLength = 10)]
    public string Grade { get; set; } = string.Empty;

    [Column(StringLength = 200)]
    public string GradeReason { get; set; } = string.Empty;

    [Column(StringLength = 100, IsNullable = true)]
    public string? TriggeringMetric { get; set; }

    [Column(StringLength = 50, IsNullable = true)]
    public string? BinId { get; set; }

    public bool IsSuccess { get; set; }

    [Column(StringLength = 500, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public double DurationMs { get; set; }

    [Column(StringLength = 50, IsNullable = true)]
    public string? RecipeId { get; set; }

    [Column(StringLength = 100, IsNullable = true)]
    public string? ProductModel { get; set; }

    public bool MesUploaded { get; set; } = false;

    public DateTime SortedAt { get; set; }
}

public class SortingLogRepository : ISortingLogRepository
{
    private readonly IFreeSql _fsql;
    private readonly ILogger<SortingLogRepository> _logger;

    public SortingLogRepository(IFreeSql fsql, ILogger<SortingLogRepository> logger)
    {
        _fsql = fsql;
        _logger = logger;
    }

    public async Task SaveAsync(SortingLog log)
    {
        var entity = MapToEntity(log);
        await _fsql.Insert<SortingLogEntity>().AppendData(entity).ExecuteAffrowsAsync();
        _logger.LogDebug("分选日志已写入 CellId={CellId}", log.CellId);
    }

    public async Task<IEnumerable<SortingLog>> GetPagedAsync(int pageIndex, int pageSize)
    {
        var entities = await _fsql.Select<SortingLogEntity>()
            .OrderByDescending(e => e.SortedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return entities.Select(MapToDomain);
    }

    public async Task<SortingLog?> GetByCellIdAsync(string cellId)
    {
        var entity = await _fsql.Select<SortingLogEntity>()
            .Where(e => e.CellId == cellId)
            .ToOneAsync();
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IEnumerable<SortingLog>> GetByTimeRangeAsync(DateTime from, DateTime to)
    {
        var entities = await _fsql.Select<SortingLogEntity>()
            .Where(e => e.SortedAt >= from && e.SortedAt <= to)
            .OrderByDescending(e => e.SortedAt)
            .ToListAsync();
        return entities.Select(MapToDomain);
    }

    public async Task<Dictionary<string, int>> GetGradeStatisticsAsync(DateTime? since = null)
    {
        var result = await _fsql.Select<SortingLogEntity>()
            .WhereIf(since.HasValue, e => e.SortedAt >= since!.Value)
            .GroupBy(e => e.Grade)
            .ToListAsync(g => new { Grade = g.Key, Count = g.Count() });
        return result.ToDictionary(r => r.Grade, r => (int)r.Count);
    }

    public async Task<IEnumerable<SortingLog>> GetRecentAsync(int count = 50)
    {
        var entities = await _fsql.Select<SortingLogEntity>()
            .OrderByDescending(e => e.SortedAt)
            .Take(count)
            .ToListAsync();
        return entities.Select(MapToDomain);
    }

    public async Task<IEnumerable<SortingLog>> GetPendingMesUploadAsync()
    {
        var entities = await _fsql.Select<SortingLogEntity>()
            .Where(e => e.IsSuccess && !e.MesUploaded)
            .ToListAsync();
        return entities.Select(MapToDomain);
    }

    public async Task<long> GetTotalCountAsync()
    {
        return await _fsql.Select<SortingLogEntity>().CountAsync();
    }

    private static SortingLogEntity MapToEntity(SortingLog log)
        => new()
        {
            CellId = log.CellId,
            OcvVoltage = log.OcvVoltage,
            IrResistance = log.IrResistance,
            Grade = log.Grade,
            GradeReason = log.GradeReason,
            TriggeringMetric = log.TriggeringMetric,
            BinId = log.BinId,
            IsSuccess = log.IsSuccess,
            ErrorMessage = log.ErrorMessage,
            DurationMs = log.DurationMs,
            RecipeId = log.RecipeId,
            ProductModel = log.ProductModel,
            SortedAt = log.SortedAt
        };

    private static SortingLog MapToDomain(SortingLogEntity entity)
        => new()
        {
            Id = entity.Id,
            CellId = entity.CellId,
            OcvVoltage = entity.OcvVoltage,
            IrResistance = entity.IrResistance,
            Grade = entity.Grade,
            GradeReason = entity.GradeReason,
            TriggeringMetric = entity.TriggeringMetric,
            BinId = entity.BinId,
            IsSuccess = entity.IsSuccess,
            ErrorMessage = entity.ErrorMessage,
            DurationMs = entity.DurationMs,
            RecipeId = entity.RecipeId,
            ProductModel = entity.ProductModel,
            SortedAt = entity.SortedAt
        };
}
