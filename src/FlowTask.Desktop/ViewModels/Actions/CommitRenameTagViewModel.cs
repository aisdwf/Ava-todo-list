using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 提交标签重命名，并保留所有任务关联（TR-1）。
/// </summary>
public sealed class CommitRenameTagViewModel
{
    private readonly ITagRepository _tagRepository;

    public CommitRenameTagViewModel(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task ExecuteAsync(
        TagItemViewModel? tag,
        IEnumerable<(string Id, string Name)> existingTags,
        Func<Task> reloadTasks)
    {
        if (tag is null)
        {
            return;
        }

        if (!TagName.IsValid(tag.RenameBuffer))
        {
            tag.CancelRename();
            return;
        }

        var normalizedName = TagName.Normalize(tag.RenameBuffer);
        if (existingTags.Any(existing =>
                existing.Id != tag.Id
                && string.Equals(existing.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            tag.CancelRename();
            return;
        }

        tag.Tag.Name = normalizedName;
        await _tagRepository.SaveAsync(tag.Tag);
        tag.SyncFromEntity();
        tag.CancelRename();
        await reloadTasks();
    }
}
