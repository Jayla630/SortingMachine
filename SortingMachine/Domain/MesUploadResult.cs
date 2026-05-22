// =========================================================
// File: Domain/MesUploadResult.cs
// Project: SortingMachine
// Sprint: S5 | Agent: Claude Code
// =========================================================


namespace SortingMachine.Domain;

public record MesUploadResult
{
    public bool IsSuccess { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime UploadedAt { get; init; } = DateTime.Now;

    public static MesUploadResult Success(int count)
        => new() { IsSuccess = true, SuccessCount = count };

    public static MesUploadResult Failure(string error, int failed)
        => new() { IsSuccess = false, FailedCount = failed, ErrorMessage = error };
}
