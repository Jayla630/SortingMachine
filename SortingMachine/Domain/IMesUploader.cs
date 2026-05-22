// =========================================================
// File: Domain/IMesUploader.cs
// Project: SortingMachine
// Sprint: S5 | Agent: Claude Code
// =========================================================

namespace SortingMachine.Domain;

public interface IMesUploader
{
    /// <summary>
    /// 批量上报分选结果到 MES。
    /// 内部从 ISortingLogRepository.GetPendingMesUploadAsync() 获取待上报记录，
    /// 上报成功后调用 MarkAsUploadedAsync 标记。
    /// </summary>
    Task<MesUploadResult> UploadPendingAsync(CancellationToken ct = default);

    /// <summary>上报单条记录（手动重试用）</summary>
    Task<MesUploadResult> UploadSingleAsync(SortingLog log, CancellationToken ct = default);

    /// <summary>检查 MES 连通性</summary>
    Task<bool> PingAsync(CancellationToken ct = default);

    /// <summary>MES 服务端地址（用于 UI 显示）</summary>
    string Endpoint { get; }
}

#region DesignNotes

// ── 为什么用 Mock/Real 双实现而不是 Mock 参数？ ──
// Mock 参数（如 bool isMock）会让接口实现充斥着 if/else 分支，违反单一职责。
// 双实现（MockMesUploadService + MesUploadService）通过 DI 切换，代码干净、
// 测试时无需模拟 HTTP 层，产线部署时替换注册即可。
// 生活类比：快递站试运行期间用 Excel 表格记账（Mock），
// 系统对接完毕后换回总部揽收系统（Real），两套账本格式一致，切换无感。

// ── 为什么批量上报而不是逐条实时上报？ ──
// 分选节拍 ~1s/颗，HTTP 往返延迟不可控（50-500ms），逐条上报会拖慢产线。
// 批量上报由后台定时器触发（如每 30s），攒够一批再发，失败整批重试，
// 减少了 HTTP 连接开销和对 MES 服务器的压力。
// 生活类比：快递员不会收一件送一件，而是攒满一车再回站点。

// ── 为什么 MarkAsUploaded 在 HTTP 成功后才调用？ ──
// 先标记后上报会导致 "标记了但 MES 没收到" 的数据丢失。
// 先上报后标记：HTTP 返回 200 确认 MES 已落库，再标记 MesUploaded=true，
// 即使标记时程序崩溃，下次启动会重复上报同一条（MES 端做幂等去重即可）。
// 生活类比：快递签收后才在底单上盖"已送达"，不会先盖章再送货。

#endregion
