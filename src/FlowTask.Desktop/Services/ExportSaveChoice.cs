namespace FlowTask.Desktop.Services;

/// <summary>
/// 保存对话框的一次确认。取消由 <see cref="IExportSavePicker.PickAsync"/> 返回 <c>null</c> 表示。
/// </summary>
public sealed class ExportSaveChoice
{
    /// <summary>
    /// 本地文件路径。沙盒里拿不到路径时为 <c>null</c>，改走 <see cref="OpenWriteAsync"/>。
    /// </summary>
    public string? LocalPath { get; init; }

    /// <summary>拿不到本地路径时打开目标写入流。</summary>
    public Func<Task<Stream>>? OpenWriteAsync { get; init; }
}

/// <summary>
/// 选择导出文件的保存位置。实现留在视图层：对话框需要窗口，视图模型不引用 Avalonia。
/// </summary>
public interface IExportSavePicker
{
    /// <summary>
    /// 弹出保存对话框。
    /// </summary>
    /// <param name="suggestedFileName">默认文件名，含本地日期。</param>
    /// <returns>用户确认后的位置；取消时为 <c>null</c>。</returns>
    Task<ExportSaveChoice?> PickAsync(string suggestedFileName);
}
