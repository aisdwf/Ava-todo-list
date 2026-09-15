using SQLite;

namespace FlowTask.Core.Models;

/// <summary>
/// 应用级键值设置行。
/// </summary>
[Table("AppSettings")]
public class AppSetting
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
