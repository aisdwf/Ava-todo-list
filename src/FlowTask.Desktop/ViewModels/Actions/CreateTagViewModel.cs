using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Appearance;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 新建标签：校验名称唯一性并落库（TR-1）。
/// </summary>
public sealed class CreateTagViewModel
{
    private readonly ITagRepository _tagRepository;
    private readonly IClock _clock;

    public CreateTagViewModel(ITagRepository tagRepository, IClock clock)
    {
        _tagRepository = tagRepository;
        _clock = clock;
    }

    public async Task ExecuteAsync(
        string name,
        IEnumerable<string> existingNames,
        int sortOrder,
        Action clearInput,
        Func<Task> reloadTags)
    {
        if (!TagName.IsValid(name))
        {
            return;
        }

        var normalizedName = TagName.Normalize(name);
        if (existingNames.Any(existing =>
                string.Equals(existing, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var tag = new Tag
        {
            Name = normalizedName,
            ColorHex = AppearanceCoordinator.PickPaletteColor(sortOrder),
            SortOrder = sortOrder,
            CreatedAt = _clock.UtcNow
        };

        await _tagRepository.SaveAsync(tag);
        clearInput();
        await reloadTags();
    }
}
