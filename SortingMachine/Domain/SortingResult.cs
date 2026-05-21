// =========================================================
// File: Domain/SortingResult.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

using SortingMachine.Domain.Recipe;

namespace SortingMachine.Domain;

/// <summary>
/// 分选操作结果 —— 记录单颗电芯分选的全量追溯信息。
/// </summary>
public record SortingResult
{
    public string CellId { get; init; } = string.Empty;
    public CellGrade Grade { get; init; }
    public string? BinId { get; init; }                      // null = 未入仓（失败）
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime SortedAt { get; init; } = DateTime.Now;

    /// <summary>追溯用：保存判级依据</summary>
    public GradeDecision? GradeDecision { get; init; }

    public static SortingResult Success(string cellId, CellGrade grade,
        string binId, GradeDecision decision, TimeSpan duration)
        => new()
        {
            CellId = cellId,
            Grade = grade,
            BinId = binId,
            IsSuccess = true,
            GradeDecision = decision,
            Duration = duration
        };

    public static SortingResult Failure(string cellId, string errorMessage, TimeSpan duration)
        => new()
        {
            CellId = cellId,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Duration = duration
        };
}

/// <summary>
/// 分选完成事件参数（成功或失败均触发）。
/// </summary>
public record SortingCompletedEventArgs(SortingResult Result, BinDefinition? Bin);

/// <summary>
/// 料仓满料事件参数。
/// </summary>
public record BinFullEventArgs(BinDefinition Bin, CellGrade Grade);
