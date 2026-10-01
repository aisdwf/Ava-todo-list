using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 数据库路径只有一处权威定义（扫描 M4）。
/// </summary>
public class DatabaseLocationTests : IDisposable
{
    private readonly string? _previousPath;
    private readonly string? _previousPreview;
    private readonly string _previousRoot;

    public DatabaseLocationTests()
    {
        _previousPath = Environment.GetEnvironmentVariable(DatabaseLocation.PathEnvironmentVariable);
        _previousPreview = Environment.GetEnvironmentVariable(DatabaseLocation.PreviewEnvironmentVariable);
        _previousRoot = DatabaseLocation.RootDirectory;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DatabaseLocation.PathEnvironmentVariable, _previousPath);
        Environment.SetEnvironmentVariable(DatabaseLocation.PreviewEnvironmentVariable, _previousPreview);
        DatabaseLocation.RootDirectory = _previousRoot;
    }

    [Fact]
    public void Resolve_UsesExplicitPathOverEnvironmentAndDefault()
    {
        var explicitPath = Path.Combine(Path.GetTempPath(), $"flowtask_explicit_{Guid.NewGuid():N}", "custom.db");
        Environment.SetEnvironmentVariable(
            DatabaseLocation.PathEnvironmentVariable,
            Path.Combine(Path.GetTempPath(), "should-not-use.db"));

        var resolved = DatabaseLocation.Resolve(explicitPath);

        Assert.Equal(explicitPath, resolved);
        Assert.True(Directory.Exists(Path.GetDirectoryName(resolved)));
    }

    [Fact]
    public void Resolve_UsesEnvironmentVariableWhenNoExplicitPath()
    {
        var envPath = Path.Combine(Path.GetTempPath(), $"flowtask_env_{Guid.NewGuid():N}", "from-env.db");
        Environment.SetEnvironmentVariable(DatabaseLocation.PathEnvironmentVariable, envPath);

        Assert.Equal(envPath, DatabaseLocation.Resolve());
    }

    [Fact]
    public void Resolve_DefaultIsSharedByAllCallersAndNotDailyFileUnderTestHost()
    {
        Environment.SetEnvironmentVariable(DatabaseLocation.PathEnvironmentVariable, null);
        var root = Path.Combine(Path.GetTempPath(), $"flowtask_root_{Guid.NewGuid():N}");
        DatabaseLocation.RootDirectory = root;

        var first = DatabaseLocation.Resolve();
        var second = DatabaseLocation.Resolve();

        Assert.Equal(first, second);
        Assert.Equal("flowtask.preview.db", Path.GetFileName(first));
        Assert.NotEqual("flowtask.db", Path.GetFileName(first));
        Assert.StartsWith(root, first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewEnvironment_ForcesPreviewFileName()
    {
        Environment.SetEnvironmentVariable(DatabaseLocation.PathEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(DatabaseLocation.PreviewEnvironmentVariable, "1");
        DatabaseLocation.RootDirectory = Path.Combine(Path.GetTempPath(), $"flowtask_prev_{Guid.NewGuid():N}");

        Assert.True(DatabaseLocation.IsolatesFromDailyDatabase());
        Assert.Equal("flowtask.preview.db", Path.GetFileName(DatabaseLocation.Resolve()));
    }
}
