using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 设置 → 命令行的四项开关。每项变更立即写入 <see cref="IAppSettingsRepository"/>。
/// </summary>
public partial class CliAccessSettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsRepository _settings;
    private bool _suppress;

    /// <summary>最近一次落库。测试等待它，避免勾选后立刻读库仍是旧值。</summary>
    public Task LastPersist { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private bool _allowRead;

    [ObservableProperty]
    private bool _allowWrite;

    [ObservableProperty]
    private bool _markWrites = true;

    /// <summary>总开关打开后，读和写才可以改。</summary>
    public bool CanConfigurePermissions => Enabled;

    public CliAccessSettingsViewModel(IAppSettingsRepository settings)
    {
        _settings = settings;
    }

    /// <summary>从设置读回。缺省与 <see cref="CliAccessPolicy.Default"/> 一致。</summary>
    public async Task LoadAsync()
    {
        _suppress = true;
        try
        {
            Enabled = CliAccessPolicy.ReadBool(
                await _settings.GetAsync(CliAccessPolicy.EnabledKey),
                CliAccessPolicy.Default.Enabled);
            AllowRead = CliAccessPolicy.ReadBool(
                await _settings.GetAsync(CliAccessPolicy.AllowReadKey),
                CliAccessPolicy.Default.AllowRead);
            AllowWrite = CliAccessPolicy.ReadBool(
                await _settings.GetAsync(CliAccessPolicy.AllowWriteKey),
                CliAccessPolicy.Default.AllowWrite);
            MarkWrites = CliAccessPolicy.ReadBool(
                await _settings.GetAsync(CliAccessPolicy.MarkWritesKey),
                CliAccessPolicy.Default.MarkWrites);
        }
        finally
        {
            _suppress = false;
        }
    }

    partial void OnEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConfigurePermissions));
        Queue(CliAccessPolicy.EnabledKey, value);
    }

    partial void OnAllowReadChanged(bool value) => Queue(CliAccessPolicy.AllowReadKey, value);

    partial void OnAllowWriteChanged(bool value) => Queue(CliAccessPolicy.AllowWriteKey, value);

    partial void OnMarkWritesChanged(bool value) => Queue(CliAccessPolicy.MarkWritesKey, value);

    private void Queue(string key, bool value)
    {
        if (_suppress)
        {
            return;
        }

        LastPersist = _settings.SetAsync(key, CliAccessPolicy.StoreBool(value));
    }
}
