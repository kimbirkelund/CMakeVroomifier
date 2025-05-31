using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CMakeVroomifier.Cli;
using CommandLine;
using static CMakeVroomifier.Cli.ConsoleHelpers;

return Parser.Default
             .ParseArguments<Options>(args)
             .MapResult(
                 RunWithOptions,
                 _ => 1);

static int RunWithOptions(Options opts)
{
    WriteHeader("options", ConsoleColor.DarkGreen);
    Console.WriteLine($"Path: {opts.Path}");
    Console.WriteLine($"ConfigurePreset: {opts.ConfigurePreset}");
    Console.WriteLine($"BuildPreset: {opts.BuildPreset}");
    Console.WriteLine($"TestPreset: {opts.TestPreset}");
    Console.WriteLine($"ExcludeTests: {opts.ExcludeTests}");

    using var fileWatcher = new FileWatcher(opts.Path,
                                            ["*.cpp", "*.h", "*.c", "*.hpp", "*.rc", "*.qrc", "CMakeLists.txt", "*.cmake", "*.ui"],
                                            ["build/", ".vs/"]);

    BehaviorSubject<CancellationTokenSource> cts = new(new CancellationTokenSource());
    var bufferCloser = new BehaviorSubject<Unit>(Unit.Default);
    var changes = fileWatcher.Changes
                             .Do(_ => cts.Value.Cancel())
                             .Buffer(bufferCloser)
                             .ToEnumerable();

    foreach (var changedFiles in changes)
    {
        if (changedFiles.Any())
        {
            cts.OnNext(new CancellationTokenSource());

            WriteHeader("Changes detected");
            Console.WriteLine(changedFiles.Select(f => $" - {f}").Join(Environment.NewLine));
        }

        cts.Value.Token.WaitHandle.WaitOne(Timeout.Infinite);
        bufferCloser.OnNext(Unit.Default);
    }

    return 0;
}
