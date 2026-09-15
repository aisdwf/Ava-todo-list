using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 任务标签选择器中的单个选项。
/// </summary>
public partial class TagChoice : ViewModelBase
{
    /// <summary>对应的受管理标签。</summary>
    public Tag Tag { get; }

    /// <summary>显示名称。</summary>
    public string Name => Tag.Name;

    /// <summary>标签色值。</summary>
    public string ColorHex => Tag.ColorHex;

    /// <summary>是否被当前任务选中。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 构造标签选择项。
    /// </summary>
    public TagChoice(Tag tag, bool isSelected = false)
    {
        Tag = tag;
        _isSelected = isSelected;
    }
}
