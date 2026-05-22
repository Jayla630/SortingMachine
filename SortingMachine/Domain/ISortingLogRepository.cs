// =========================================================
// File: Domain/ISortingLogRepository.cs
// Project: SortingMachine
// Sprint: S4 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain;

public interface ISortingLogRepository
{
    /// <summary>写入一条分选日志（异步）</summary>
    Task SaveAsync(SortingLog log);

    /// <summary>分页查询（最新在前）</summary>
    Task<IEnumerable<SortingLog>> GetPagedAsync(int pageIndex, int pageSize);

    /// <summary>按条码查询</summary>
    Task<SortingLog?> GetByCellIdAsync(string cellId);

    /// <summary>查询指定时间段内的记录</summary>
    Task<IEnumerable<SortingLog>> GetByTimeRangeAsync(DateTime from, DateTime to);

    /// <summary>统计各等级数量（用于监控页仪表盘）</summary>
    Task<Dictionary<string, int>> GetGradeStatisticsAsync(DateTime? since = null);

    /// <summary>获取最近 N 条记录（用于实时监控列表）</summary>
    Task<IEnumerable<SortingLog>> GetRecentAsync(int count = 50);

    /// <summary>获取未上报 MES 的记录</summary>
    Task<IEnumerable<SortingLog>> GetPendingMesUploadAsync();

    /// <summary>总记录数</summary>
    Task<long> GetTotalCountAsync();

    /// <summary>标记记录为已上报 MES</summary>
    Task MarkAsUploadedAsync(IEnumerable<long> ids);
}

#region DesignNotes

// ── 为什么日志用规范化列而不是 JSON 列？ ──
// 配方（SortingRecipe）以配置语义为主，读写粒度是整个配方，很少按字段查询，
// 适合 JSON 列存储（灵活扩展、减少联表）。
//
// 分选日志（SortingLog）需要按等级/时间范围/条码做聚合查询和统计，
// 规范化列可以利用 SQL 索引和 GROUP BY，查询性能和便利性远优于 JSON 列。

// ── 为什么写日志用 fire-and-forget 而不是 await？ ──
// 产线节拍优先：SQLite 写入（~5ms）不能卡在分选主链路上阻塞下一颗电芯。
// 日志写入失败只记录 Log.Error，不影响分选结果（电芯已入仓，不能因为记账失败就回滚物理动作）。

// ── 为什么 MesUploaded 字段在 S4 预置而不是 S5 才加？ ──
// 数据库 Schema 变更需要迁移策略。预置字段（默认 false）避免 S5 做 ALTER TABLE，
// 也避免了"分选日志已写入但 MES 上报状态缺失"的数据不完整窗口。

#endregion
