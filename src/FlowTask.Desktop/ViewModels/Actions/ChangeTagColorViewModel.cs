using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Appearance;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 循环切换标签色并刷新列表与任务流（TR-1）。
/// </summary>
public sealed class ChangeTagColorViewModel
{
    private readonly ITagRepository _tagRepository;

    public ChangeTagColorViewModel(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task ExecuteAsync(
        TagItemViewModel? tag,
        Func<Task> reloadTags,
        Func<Task> reloadTasks)
    {
        if (tag is null)
        {
            return;
        }

        tag.Tag.ColorHex = AppearanceCoordinator.CyclePaletteColor(tag.ColorHex);
        await _tagRepository.SaveAsync(tag.Tag);
        tag.SyncFromEntity();
        await reloadTags();
        await reloadTasks();
    }
}
