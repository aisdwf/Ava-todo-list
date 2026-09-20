using FlowTask.Core.Interfaces;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 删除标签并清理关联，随后刷新列表与任务流（TR-1）。
/// </summary>
public sealed class DeleteTagViewModel
{
    private readonly ITagRepository _tagRepository;

    public DeleteTagViewModel(ITagRepository tagRepository)
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

        await _tagRepository.DeleteAsync(tag.Id);
        await reloadTags();
        await reloadTasks();
    }
}
