using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 用主窗的系统保存对话框选择导出路径。
/// </summary>
/// <remarks>
/// 对话框必须挂在 <see cref="TopLevel"/> 上，所以由视图在构造窗口时赋给视图模型，
/// 视图模型本身不引用 Avalonia。
/// </remarks>
public sealed class WindowExportSavePicker : IExportSavePicker
{
    private readonly Window _window;

    public WindowExportSavePicker(Window window)
    {
        _window = window;
    }

    /// <inheritdoc />
    public async Task<ExportSaveChoice?> PickAsync(string suggestedFileName)
    {
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出为 JSON",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        if (file is null)
        {
            return null;
        }

        var path = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            return new ExportSaveChoice { LocalPath = path };
        }

        return new ExportSaveChoice { OpenWriteAsync = () => file.OpenWriteAsync() };
    }
}
