using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;
using FlowTask.Infrastructure.Export;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 设置 → 数据：把全部项目与任务导出为 JSON（TR-1）。
/// </summary>
public partial class ExportDataViewModel : ViewModelBase
{
    private readonly JsonDataExporter _exporter;
    private readonly IClock _clock;

    public ExportDataViewModel(JsonDataExporter exporter, IClock clock)
    {
        _exporter = exporter;
        _clock = clock;
    }

    /// <summary>
    /// 保存对话框。由主窗赋上；测试替换为假实现。未赋值时导出会说明原因，而不是空引用。
    /// </summary>
    public IExportSavePicker? SavePicker { get; set; }

    /// <summary>按钮下方的结果或失败原因。取消对话框时保持原值。</summary>
    [ObservableProperty]
    private string? _resultText;

    /// <summary>还没有结果时不占一行。</summary>
    public bool HasResult => !string.IsNullOrEmpty(ResultText);

    /// <summary>默认文件名。日期取 <see cref="IClock.Today"/>，不是 UTC 的日期。</summary>
    public string SuggestedFileName =>
        string.Create(CultureInfo.InvariantCulture, $"flowtask-export-{_clock.Today:yyyyMMdd}.json");

    partial void OnResultTextChanged(string? value)
        => OnPropertyChanged(nameof(HasResult));

    [RelayCommand]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        var picker = SavePicker;
        if (picker is null)
        {
            ResultText = "无法打开保存对话框。";
            return;
        }

        ExportSaveChoice? choice;
        try
        {
            choice = await picker.PickAsync(SuggestedFileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ResultText = Reason(ex);
            return;
        }

        if (choice is null)
        {
            return;
        }

        try
        {
            // 先在内存里生成完整字节。序列化失败时还没碰目标文件。
            using var buffer = new MemoryStream();
            var written = await _exporter.WriteAsync(buffer, cancellationToken);
            await ExportFileWriter.WriteAsync(choice, buffer.ToArray(), cancellationToken);
            ResultText = $"已导出 {written.ProjectCount} 个项目、{written.TaskCount} 条任务";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ResultText = Reason(ex);
        }
    }

    private static string Reason(Exception ex)
        => string.IsNullOrWhiteSpace(ex.Message) ? "导出失败。" : ex.Message;
}
