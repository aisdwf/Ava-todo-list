using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 设置页单个标签的展示与编辑状态。
/// </summary>
public partial class TagItemViewModel : ViewModelBase
{
    /// <summary>被包装的标签实体。</summary>
    public Tag Tag { get; }

    /// <summary>标签 Id。</summary>
    public string Id => Tag.Id;

    /// <summary>标签名。</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>标签色值。</summary>
    [ObservableProperty]
    private string _colorHex;

    /// <summary>未删除任务中的使用次数。</summary>
    [ObservableProperty]
    private int _usageCount;

    /// <summary>是否处于原地重命名状态。</summary>
    [ObservableProperty]
    private bool _isRenaming;

    /// <summary>重命名输入缓冲。</summary>
    [ObservableProperty]
    private string _renameBuffer = string.Empty;

    /// <summary>构造标签行。</summary>
    public TagItemViewModel(Tag tag, int usageCount)
    {
        Tag = tag;
        _name = tag.Name;
        _colorHex = tag.ColorHex;
        _usageCount = usageCount;
    }

    /// <summary>进入原地重命名。</summary>
    public void BeginRename()
    {
        RenameBuffer = Name;
        IsRenaming = true;
    }

    /// <summary>取消原地重命名。</summary>
    public void CancelRename()
    {
        RenameBuffer = string.Empty;
        IsRenaming = false;
    }

    /// <summary>同步实体的最新值。</summary>
    public void SyncFromEntity()
    {
        Name = Tag.Name;
        ColorHex = Tag.ColorHex;
    }
}
