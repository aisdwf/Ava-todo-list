using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 设置 → 操作指南页：左侧条目列表、右侧动效舞台（spec-onboarding-guide）。
/// </summary>
public partial class GuideViewModel : ViewModelBase
{
    /// <summary>全部指南条目。</summary>
    public IReadOnlyList<GuideTopic> Topics => GuideCatalog.Topics;

    /// <summary>当前展示的条目。</summary>
    [ObservableProperty]
    private GuideTopic? _selectedTopic = GuideCatalog.Topics[0];

    /// <summary>
    /// ListBox 失去选中会写回 null，右侧舞台随之变空。始终保留一个条目可看。
    /// </summary>
    partial void OnSelectedTopicChanged(GuideTopic? value)
    {
        if (value is null)
        {
            SelectedTopic = Topics[0];
        }
    }
}
