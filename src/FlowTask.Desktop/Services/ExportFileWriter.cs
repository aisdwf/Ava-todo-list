namespace FlowTask.Desktop.Services;

/// <summary>
/// 把已经序列化好的导出字节落到用户选定的位置。
/// </summary>
/// <remarks>
/// 有本地路径时，先在同一目录写完旁路文件再替换。覆盖旧备份时，
/// 写到一半失败不能把旧文件截断。拿不到路径时退化为直接写流；
/// 字节在这之前已经完整生成，序列化失败不会打开目标。
/// </remarks>
public static class ExportFileWriter
{
    public static async Task WriteAsync(
        ExportSaveChoice choice,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(choice.LocalPath))
        {
            await WriteToPathAsync(choice.LocalPath, bytes, cancellationToken);
            return;
        }

        if (choice.OpenWriteAsync is null)
        {
            throw new InvalidOperationException("没有可写入的导出位置。");
        }

        await using var stream = await choice.OpenWriteAsync().ConfigureAwait(false);
        await stream.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteToPathAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("导出路径没有所在目录。");
        }

        // 与目标同目录，替换才不会跨卷复制到一半。
        var stagingPath = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".flowtask-export");
        try
        {
            await File.WriteAllBytesAsync(stagingPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(stagingPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(stagingPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 清理失败不能盖住原来的写入错误。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
