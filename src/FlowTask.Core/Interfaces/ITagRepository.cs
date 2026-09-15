using FlowTask.Core.Models;

namespace FlowTask.Core.Interfaces;

/// <summary>
/// 标签及任务标签关联仓储契约。
/// </summary>
public interface ITagRepository
{
    /// <summary>初始化标签表、关联表并注入缺失的内置标签。</summary>
    Task InitializeAsync();

    /// <summary>按展示顺序载入全部标签。</summary>
    Task<List<Tag>> GetAllAsync();

    /// <summary>按任务 Id 批量载入标签，结果键为任务 Id。</summary>
    Task<Dictionary<string, List<Tag>>> GetTagsForTasksAsync(IEnumerable<string> taskIds);

    /// <summary>返回每个标签在未删除任务中的使用次数。</summary>
    Task<Dictionary<string, int>> GetUsageCountsAsync();

    /// <summary>新增或更新标签。</summary>
    Task<int> SaveAsync(Tag tag);

    /// <summary>删除标签及其全部任务关联。</summary>
    Task<int> DeleteAsync(string tagId);

    /// <summary>替换任务的全部标签关联。</summary>
    Task ReplaceTaskTagsAsync(string taskId, IEnumerable<string> tagIds);
}
