namespace FlowTask.Desktop.Services;

/// <summary>一次注册请求的结果。</summary>
public enum HotkeyRegistrationOutcome
{
    /// <summary>已注册并生效。</summary>
    Registered,

    /// <summary>组合已被其他程序占用（Win32 <c>ERROR_HOTKEY_ALREADY_REGISTERED</c>）。</summary>
    Occupied,

    /// <summary>其他失败：消息线程未就绪、超时或未知 Win32 错误。详情写入 <see cref="AppLog"/>。</summary>
    Failed,

    /// <summary>当前平台还不支持自定义组合。</summary>
    Unsupported
}

/// <summary>
/// 进程级快捷小窗快捷键的注册能力（spec-quick-window-custom-hotkey）。
/// </summary>
/// <remarks>
/// 从 <see cref="GlobalHotkeyService"/> 抽出接口：启动占用、换键失败保留旧键这些分支
/// 必须能在单测里用假实现走到，真实 <c>RegisterHotKey</c> 无法在测试里制造占用。
/// </remarks>
public interface IQuickWindowHotkeyRegistrar
{
    /// <summary>
    /// 是否支持用户自定义组合。Windows 为 true；macOS 暂为 false，
    /// 待适配（TODO(macos-custom-hotkey)，登记于 spec-macos-initial-support）。
    /// </summary>
    bool SupportsCustomHotkey { get; }

    /// <summary>
    /// 注册 <paramref name="hotkey"/>。成功后才注销此前生效的组合；失败时此前的组合保持生效。
    /// 不支持自定义的平台返回 <see cref="HotkeyRegistrationOutcome.Unsupported"/>。
    /// </summary>
    Task<HotkeyRegistrationOutcome> TryApplyAsync(QuickWindowHotkey hotkey);

    /// <summary>
    /// 不支持自定义的平台注册其固定组合（macOS 为 Option+Space）。返回是否注册成功；
    /// 失败时调用方走窗内回退。支持自定义的平台不调用本方法。
    /// </summary>
    Task<bool> TryStartFixedAsync();
}

/// <summary>没有进程级热键能力的注册器：测试默认值，也是 Windows / macOS 以外平台的行为。</summary>
public sealed class NoSystemHotkeyRegistrar : IQuickWindowHotkeyRegistrar
{
    /// <summary>共享实例。</summary>
    public static NoSystemHotkeyRegistrar Instance { get; } = new();

    /// <inheritdoc />
    public bool SupportsCustomHotkey => false;

    /// <inheritdoc />
    public Task<HotkeyRegistrationOutcome> TryApplyAsync(QuickWindowHotkey hotkey)
        => Task.FromResult(HotkeyRegistrationOutcome.Unsupported);

    /// <inheritdoc />
    public Task<bool> TryStartFixedAsync() => Task.FromResult(false);
}
