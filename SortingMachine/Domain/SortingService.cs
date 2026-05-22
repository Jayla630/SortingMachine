// =========================================================
// File: Domain/SortingService.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Claude Code
// =========================================================

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain.Recipe;
using SortingMachine.Domain.StateMachines;
using SortingMachine.Infrastructure.Motion;

namespace SortingMachine.Domain;

/// <summary>
/// 分选核心领域服务 —— 编排单颗电芯从"检测数据"到"入仓计数"的完整流程。
/// 生活类比：快递分拣员，拿到包裹 → 对照规则 → 走到对应格子 → 放进去 → 记录。
/// </summary>
public class SortingService : ISortingService
{
    private readonly IMotionController _motion;
    private readonly ISafetyValidator _safety;
    private readonly ISortingLogRepository _logRepository;
    private readonly ILogger<SortingService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SortingRecipe? ActiveRecipe { get; private set; }

    public event EventHandler<SortingCompletedEventArgs>? SortingCompleted;
    public event EventHandler<BinFullEventArgs>? BinFull;

    public SortingService(
        IMotionController motionController,
        ISafetyValidator safetyValidator,
        ISortingLogRepository logRepository,
        ILogger<SortingService> logger)
    {
        _motion = motionController;
        _safety = safetyValidator;
        _logRepository = logRepository;
        _logger = logger;
    }

    public void LoadRecipe(SortingRecipe recipe)
    {
        _logger.LogInformation("加载配方 {RecipeId} ({ProductModel}), {BinCount} 个料仓",
            recipe.RecipeId, recipe.ProductModel, recipe.Bins.Count);
        ActiveRecipe = recipe;
    }

    public void UnloadRecipe()
    {
        _logger.LogInformation("卸载配方 {RecipeId}", ActiveRecipe?.RecipeId);
        ActiveRecipe = null;
    }

    public async Task<bool> IsReadyAsync()
    {
        if (ActiveRecipe == null)
        {
            _logger.LogWarning("分选服务未就绪：未加载配方");
            return false;
        }

        var allAxesReady = await _motion.IsAllAxesReadyAsync();
        if (!allAxesReady)
        {
            _logger.LogWarning("分选服务未就绪：运动控制器未就绪");
            return false;
        }

        return true;
    }

    public void ResetBinCounts()
    {
        if (ActiveRecipe == null) return;

        foreach (var bin in ActiveRecipe.Bins)
        {
            bin.CurrentCount = 0;
        }
        _logger.LogInformation("已重置所有料仓计数");
    }

    public async Task<SortingResult> SortCellAsync(CellMeasurement measurement,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(30), ct))
        {
            _logger.LogError("分选锁获取超时，可能上一颗电芯运动卡死");
            return SortingResult.Failure(measurement.CellId, "系统繁忙：上一颗电芯分选未完成", sw.Elapsed);
        }

        try
        {
            return await SortCellInternalAsync(measurement, sw, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<SortingResult> SortCellInternalAsync(CellMeasurement measurement,
        Stopwatch sw, CancellationToken ct)
    {
        // ── 1. 前置检查 ──
        if (!await IsReadyAsync())
            return SortingResult.Failure(measurement.CellId, "分选服务未就绪", sw.Elapsed);

        var recipe = ActiveRecipe!;

        // ── 2. 判级 ──
        var gradeDecision = recipe.GradingRules.DetermineGrade(
            measurement.OcvVoltage, measurement.IrResistance);
        _logger.LogInformation("电芯 {CellId} 判级: {Grade} ({Reason})",
            measurement.CellId, gradeDecision.Grade, gradeDecision.Reason);

        // ── 3. 选仓 ──
        var targetBin = recipe.Bins
            .FirstOrDefault(b => b.TargetGrade == gradeDecision.Grade && !b.IsFull);

        if (targetBin == null)
        {
            var fullBin = recipe.Bins.FirstOrDefault(b => b.TargetGrade == gradeDecision.Grade);
            if (fullBin != null)
            {
                BinFull?.Invoke(this, new BinFullEventArgs(fullBin, gradeDecision.Grade));
                return SortingResult.Failure(measurement.CellId,
                    $"料仓已满: {fullBin.BinId}", sw.Elapsed);
            }
            return SortingResult.Failure(measurement.CellId,
                $"无可用料仓接收等级 {gradeDecision.Grade}", sw.Elapsed);
        }

        // ── 安全校验：目标位置合法 ──
        var statuses = await GetAllAxisStatusesAsync();
        var safetyX = await _safety.ValidateMoveAsync(AxisId.X, targetBin.X, statuses);
        if (!safetyX.IsSafe)
            return SortingResult.Failure(measurement.CellId,
                $"X 轴安全校验失败: {safetyX.ViolationReason}", sw.Elapsed);

        var safetyY = await _safety.ValidateMoveAsync(AxisId.Y, targetBin.Y, statuses);
        if (!safetyY.IsSafe)
            return SortingResult.Failure(measurement.CellId,
                $"Y 轴安全校验失败: {safetyY.ViolationReason}", sw.Elapsed);

        // ── 4. 运动序列 ──
        var mp = recipe.MotionParameters;
        var xyVelocity = mp.XyVelocity;
        var zVelocity = mp.ZVelocity;
        var safeZ = mp.SafeZHeight;

        // a. Z 轴抬起到安全高度
        var result = await _motion.MoveAbsoluteAsync(AxisId.Z, safeZ, zVelocity, ct);
        if (!result.IsSuccess)
        {
            await _motion.EmergencyStopAsync();
            return SortingResult.Failure(measurement.CellId,
                $"Z 轴抬升失败: {result.ErrorMessage}", sw.Elapsed);
        }

        // b. X 轴移动到料仓 X 坐标
        result = await _motion.MoveAbsoluteAsync(AxisId.X, targetBin.X, xyVelocity, ct);
        if (!result.IsSuccess)
        {
            await _motion.EmergencyStopAsync();
            return SortingResult.Failure(measurement.CellId,
                $"X 轴运动失败: {result.ErrorMessage}", sw.Elapsed);
        }

        // c. Y 轴移动到料仓 Y 坐标
        result = await _motion.MoveAbsoluteAsync(AxisId.Y, targetBin.Y, xyVelocity, ct);
        if (!result.IsSuccess)
        {
            await _motion.EmergencyStopAsync();
            return SortingResult.Failure(measurement.CellId,
                $"Y 轴运动失败: {result.ErrorMessage}", sw.Elapsed);
        }

        // d. Z 轴下降到放料高度
        result = await _motion.MoveAbsoluteAsync(AxisId.Z, targetBin.ZPickHeight, zVelocity, ct);
        if (!result.IsSuccess)
        {
            await _motion.EmergencyStopAsync();
            return SortingResult.Failure(measurement.CellId,
                $"Z 轴下降失败: {result.ErrorMessage}", sw.Elapsed);
        }

        // e. 模拟放料动作
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            // 放料已触发但被取消，尝试抬 Z 回安全位
            await _motion.MoveAbsoluteAsync(AxisId.Z, safeZ, zVelocity, CancellationToken.None);
            return SortingResult.Failure(measurement.CellId, "分选被取消", sw.Elapsed);
        }

        // f. Z 轴抬起回安全高度
        result = await _motion.MoveAbsoluteAsync(AxisId.Z, safeZ, zVelocity, ct);
        if (!result.IsSuccess)
        {
            await _motion.EmergencyStopAsync();
            return SortingResult.Failure(measurement.CellId,
                $"Z 轴回抬失败: {result.ErrorMessage}", sw.Elapsed);
        }

        // ── 5. 收尾 ──
        targetBin.CurrentCount++;
        var sortingResult = SortingResult.Success(measurement.CellId, gradeDecision.Grade,
            targetBin.BinId, gradeDecision, sw.Elapsed);

        _logger.LogInformation("电芯 {CellId} → {BinId} ({Grade}), 耗时 {Duration}ms",
            measurement.CellId, targetBin.BinId, gradeDecision.Grade, sw.ElapsedMilliseconds);

        SortingCompleted?.Invoke(this, new SortingCompletedEventArgs(sortingResult, targetBin));

        // 分选完成，写日志（fire-and-forget，不阻塞分选节拍）
        var log = SortingLog.FromResult(sortingResult, measurement,
            ActiveRecipe?.RecipeId, ActiveRecipe?.ProductModel);
        _ = _logRepository.SaveAsync(log).ContinueWith(t =>
        {
            if (t.IsFaulted)
                _logger.LogError(t.Exception, "分选日志写入失败 CellId={CellId}", measurement.CellId);
        });

        if (targetBin.IsFull)
        {
            BinFull?.Invoke(this, new BinFullEventArgs(targetBin, targetBin.TargetGrade));
        }

        return sortingResult;
    }

    private async Task<IReadOnlyDictionary<AxisId, AxisStatus>> GetAllAxisStatusesAsync()
    {
        var x = await _motion.GetAxisStatusAsync(AxisId.X);
        var y = await _motion.GetAxisStatusAsync(AxisId.Y);
        var z = await _motion.GetAxisStatusAsync(AxisId.Z);
        return new Dictionary<AxisId, AxisStatus>
        {
            [AxisId.X] = x,
            [AxisId.Y] = y,
            [AxisId.Z] = z
        };
    }
}
