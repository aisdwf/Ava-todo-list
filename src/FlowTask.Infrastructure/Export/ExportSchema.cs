namespace FlowTask.Infrastructure.Export;

/// <summary>
/// 导出文件格式版本。以后导入靠它识别，不散落到调用方。
/// </summary>
public static class ExportSchema
{
    /// <summary>当前写出的 <c>schemaVersion</c>。</summary>
    public const int Current = 1;
}
