namespace FlowTask.Core.Interfaces;

/// <summary>
/// 应用设置键值仓储。
/// </summary>
public interface IAppSettingsRepository
{
    Task InitializeAsync();

    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    Task<int> GetDefaultDueOffsetDaysAsync();

    Task SetDefaultDueOffsetDaysAsync(int days);
}
