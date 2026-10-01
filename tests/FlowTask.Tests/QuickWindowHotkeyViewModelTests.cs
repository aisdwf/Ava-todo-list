using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 快捷小窗快捷键的运行状态（spec-quick-window-custom-hotkey）：启动占用停用、换键、恢复默认。
/// </summary>
/// <remarks>
/// 真实 <c>RegisterHotKey</c> 无法在测试里制造占用，注册器用 <see cref="FakeRegistrar"/> 代替。
/// 系统级热键是否真正生效、PowerToys 占用下的表现，由所有者预览验证。
/// </remarks>
public class QuickWindowHotkeyViewModelTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_hotkey_{Guid.NewGuid():N}.db");
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    /// <inheritdoc />
    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // 测试清理阶段的文件锁不影响断言结果
            }
        }
    }

    private SqliteAppSettingsRepository Settings() => new(_dbPath);

    private QuickWindowHotkeyViewModel Create(FakeRegistrar registrar, bool isMacOS = false)
        => new(registrar, Settings(), isMacOS);

    private static QuickWindowHotkey Parse(string raw)
    {
        Assert.True(QuickWindowHotkey.TryParse(raw, out var hotkey));
        return hotkey;
    }

    private async Task SaveAsync(string raw)
    {
        var settings = Settings();
        await settings.InitializeAsync();
        await settings.SetAsync(QuickWindowHotkey.SettingsKey, raw);
    }

    private async Task<string?> ReadSavedAsync()
    {
        var settings = Settings();
        await settings.InitializeAsync();
        return await settings.GetAsync(QuickWindowHotkey.SettingsKey);
    }

    [Fact]
    public async Task Start_RegistersDefaultWhenNothingSaved()
    {
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);

        await vm.StartAsync();

        Assert.Equal(new[] { QuickWindowHotkey.Default }, registrar.Attempts);
        Assert.True(vm.IsSystemActive);
        Assert.False(vm.IsSuspended);
        Assert.Equal("Alt+Space", vm.Label);
        Assert.False(vm.IsConflictPromptOpen);
    }

    [Fact]
    public async Task Start_RegistersSavedCombination()
    {
        await SaveAsync("Ctrl+Alt+K");
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);

        await vm.StartAsync();

        Assert.Equal(new[] { Parse("Ctrl+Alt+K") }, registrar.Attempts);
        Assert.Equal("Ctrl+Alt+K", vm.Label);
    }

    [Fact]
    public async Task Start_OccupiedSuspendsPromptsOnceAndTriesNoFallback()
    {
        // 回归：此前 Alt+Space 被占用时静默改注册 Win+Alt+Space
        var registrar = new FakeRegistrar();
        registrar.Occupy(QuickWindowHotkey.Default);
        var vm = Create(registrar);

        await vm.StartAsync();

        Assert.Equal(new[] { QuickWindowHotkey.Default }, registrar.Attempts);
        Assert.False(vm.IsSystemActive);
        Assert.True(vm.IsSuspended);
        Assert.Equal("已停用", vm.Label);
        Assert.True(vm.IsConflictPromptOpen);
        Assert.Contains("Alt+Space", vm.ConflictPromptText, StringComparison.Ordinal);
        Assert.Contains("已被其他程序占用", vm.ConflictPromptText, StringComparison.Ordinal);
        Assert.False(vm.ShouldHandleInWindow(KeyModifiers.Alt, Key.Space));
    }

    [Fact]
    public async Task Start_OccupiedDoesNotOverwriteSavedValue()
    {
        await SaveAsync("Ctrl+Alt+K");
        var registrar = new FakeRegistrar();
        registrar.Occupy(Parse("Ctrl+Alt+K"));

        await Create(registrar).StartAsync();

        Assert.Equal("Ctrl+Alt+K", await ReadSavedAsync());
    }

    [Fact]
    public async Task Start_PromptsAgainOnNextLaunch()
    {
        // owner 裁决 Q4「启动提示」：每次启动遇到占用都弹一次
        var registrar = new FakeRegistrar();
        registrar.Occupy(QuickWindowHotkey.Default);

        var first = Create(registrar);
        await first.StartAsync();
        first.DismissConflictPromptCommand.Execute(null);

        var second = Create(registrar);
        await second.StartAsync();

        Assert.False(first.IsConflictPromptOpen);
        Assert.True(second.IsConflictPromptOpen);
    }

    [Fact]
    public async Task Record_AppliesAndPersistsNewCombination()
    {
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);
        await vm.StartAsync();

        vm.ToggleRecordingCommand.Execute(null);
        Assert.True(await vm.RecordAsync(KeyModifiers.Control | KeyModifiers.Alt, Key.K));

        Assert.False(vm.IsRecording);
        Assert.Equal("Ctrl+Alt+K", vm.Label);
        Assert.False(vm.IsStatusError);
        Assert.Equal("Ctrl+Alt+K", await ReadSavedAsync());
        // 系统级热键已生效：窗内不得再响应同一组合
        Assert.False(vm.ShouldHandleInWindow(KeyModifiers.Control | KeyModifiers.Alt, Key.K));
    }

    [Fact]
    public async Task Record_OccupiedKeepsPreviousCombinationAndDoesNotSave()
    {
        var registrar = new FakeRegistrar();
        registrar.Occupy(Parse("Ctrl+Alt+K"));
        var vm = Create(registrar);
        await vm.StartAsync();

        vm.ToggleRecordingCommand.Execute(null);
        await vm.RecordAsync(KeyModifiers.Control | KeyModifiers.Alt, Key.K);

        Assert.Equal("Alt+Space", vm.Label);
        Assert.True(vm.IsSystemActive);
        Assert.Equal(QuickWindowHotkey.Default, registrar.Active);
        Assert.True(vm.IsStatusError);
        Assert.Contains("已被其他程序占用", vm.StatusText, StringComparison.Ordinal);
        Assert.Null(await ReadSavedAsync());
    }

    [Fact]
    public async Task Record_ReservedComboIsRejectedWithoutRegistering()
    {
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);
        await vm.StartAsync();

        vm.ToggleRecordingCommand.Execute(null);
        await vm.RecordAsync(KeyModifiers.Control, Key.C);

        Assert.True(vm.IsRecording);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Ctrl+C", vm.StatusText, StringComparison.Ordinal);
        Assert.Single(registrar.Attempts);
    }

    [Fact]
    public async Task Record_ModifierOnlyKeepsWaitingAndEscCancels()
    {
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);
        await vm.StartAsync();

        vm.ToggleRecordingCommand.Execute(null);
        await vm.RecordAsync(KeyModifiers.Control, Key.LeftCtrl);
        Assert.True(vm.IsRecording);

        await vm.RecordAsync(KeyModifiers.None, Key.Escape);

        Assert.False(vm.IsRecording);
        Assert.False(vm.HasStatus);
        Assert.Equal("Alt+Space", vm.Label);
        Assert.Single(registrar.Attempts);
    }

    [Fact]
    public async Task ChangingWhileSuspended_RestoresHotkey()
    {
        var registrar = new FakeRegistrar();
        registrar.Occupy(QuickWindowHotkey.Default);
        var vm = Create(registrar);
        await vm.StartAsync();

        vm.ToggleRecordingCommand.Execute(null);
        await vm.RecordAsync(KeyModifiers.Control | KeyModifiers.Shift, Key.Space);

        Assert.False(vm.IsSuspended);
        Assert.True(vm.IsSystemActive);
        Assert.Equal("Ctrl+Shift+Space", vm.Label);
    }

    [Fact]
    public async Task RestoreDefault_ReturnsToAltSpace()
    {
        await SaveAsync("Ctrl+Alt+K");
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);
        await vm.StartAsync();

        await vm.RestoreDefaultCommand.ExecuteAsync(null);

        Assert.Equal("Alt+Space", vm.Label);
        Assert.Equal(QuickWindowHotkey.Default, registrar.Active);
        Assert.Equal("Alt+Space", await ReadSavedAsync());
    }

    [Fact]
    public async Task RecordingCurrentCombinationViaSystemHotkey_EndsRecordingWithoutToggle()
    {
        var registrar = new FakeRegistrar();
        var vm = Create(registrar);
        await vm.StartAsync();

        vm.ToggleRecordingCommand.Execute(null);

        Assert.True(vm.TryConsumeWhileRecording());
        Assert.False(vm.IsRecording);
        Assert.False(vm.TryConsumeWhileRecording());
    }

    [Fact]
    public async Task MacOS_IsReadOnlyAndUsesFixedRegistration()
    {
        // owner 裁决 Q1：macOS 留接口不实现
        var registrar = new FakeRegistrar { SupportsCustomHotkey = false, FixedResult = true };
        var vm = Create(registrar, isMacOS: true);

        await vm.StartAsync();
        vm.ToggleRecordingCommand.Execute(null);

        Assert.False(vm.CanCustomize);
        Assert.False(vm.IsRecording);
        Assert.True(vm.IsSystemActive);
        Assert.Equal("⌥ Space", vm.Label);
        Assert.Contains("待适配", vm.UnsupportedText, StringComparison.Ordinal);
        Assert.Empty(registrar.Attempts);
    }

    [Fact]
    public async Task NoSystemHotkey_FallsBackToInWindowMatch()
    {
        var registrar = new FakeRegistrar { SupportsCustomHotkey = false, FixedResult = false };
        var vm = Create(registrar, isMacOS: true);

        await vm.StartAsync();

        Assert.False(vm.IsSystemActive);
        Assert.False(vm.IsSuspended);
        Assert.True(vm.ShouldHandleInWindow(KeyModifiers.Alt, Key.Space));
        Assert.False(vm.ShouldHandleInWindow(KeyModifiers.Meta, Key.Space));
    }

    [AvaloniaFact]
    public async Task MainViewModel_ConflictPromptBlocksAndGoSettingsOpensGeneral()
    {
        var registrar = new FakeRegistrar();
        registrar.Occupy(QuickWindowHotkey.Default);
        var vm = new MainViewModel(
            new SqliteTaskRepository(_clock, _dbPath),
            new SqliteProjectRepository(_dbPath),
            _clock,
            Settings(),
            registrar);

        await vm.Hotkey.StartAsync();
        Assert.True(vm.IsBlockingOverlayOpen);

        vm.Hotkey.OpenSettingsFromPromptCommand.Execute(null);

        Assert.False(vm.IsBlockingOverlayOpen);
        Assert.True(vm.IsSettingsOpen);
        Assert.Equal(SettingsSection.General, vm.SelectedSettingsSection);
    }

    [AvaloniaFact]
    public async Task MainViewModel_LeavingSettingsCancelsRecording()
    {
        var registrar = new FakeRegistrar();
        var vm = new MainViewModel(
            new SqliteTaskRepository(_clock, _dbPath),
            new SqliteProjectRepository(_dbPath),
            _clock,
            Settings(),
            registrar);
        await vm.Hotkey.StartAsync();

        vm.ToggleSettingsCommand.Execute(null);
        vm.SelectSettingsSectionCommand.Execute(SettingsSection.General);
        vm.Hotkey.ToggleRecordingCommand.Execute(null);
        Assert.True(vm.Hotkey.IsRecording);

        vm.ToggleSettingsCommand.Execute(null);

        Assert.False(vm.Hotkey.IsRecording);
    }

    /// <summary>可编排占用的注册器：失败时保持此前组合，与 <see cref="GlobalHotkeyService"/> 契约一致。</summary>
    private sealed class FakeRegistrar : IQuickWindowHotkeyRegistrar
    {
        private readonly HashSet<QuickWindowHotkey> _occupied = new();

        public bool SupportsCustomHotkey { get; init; } = true;

        public bool FixedResult { get; init; }

        public List<QuickWindowHotkey> Attempts { get; } = new();

        public QuickWindowHotkey? Active { get; private set; }

        public void Occupy(QuickWindowHotkey hotkey) => _occupied.Add(hotkey);

        public Task<HotkeyRegistrationOutcome> TryApplyAsync(QuickWindowHotkey hotkey)
        {
            if (!SupportsCustomHotkey)
            {
                return Task.FromResult(HotkeyRegistrationOutcome.Unsupported);
            }

            Attempts.Add(hotkey);
            if (_occupied.Contains(hotkey))
            {
                return Task.FromResult(HotkeyRegistrationOutcome.Occupied);
            }

            Active = hotkey;
            return Task.FromResult(HotkeyRegistrationOutcome.Registered);
        }

        public Task<bool> TryStartFixedAsync() => Task.FromResult(FixedResult);
    }
}
