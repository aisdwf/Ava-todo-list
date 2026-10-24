using FlowTask.Cli;
using FlowTask.Core.Cli;
using FlowTask.Infrastructure.Persistence;
using FlowTask.Infrastructure.Time;

var clock = new SystemClock();
var path = DatabaseLocation.Resolve();
var app = new CliApp(
    new SqliteTaskRepository(clock, path),
    new SqliteProjectRepository(path),
    new SqliteAppSettingsRepository(path),
    clock,
    () => CliChangeChannel.TryNotifyAsync());

return await app.RunAsync(args, Console.Out, Console.Error);
