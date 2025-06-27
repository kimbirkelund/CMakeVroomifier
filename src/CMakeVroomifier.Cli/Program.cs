using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using CommandLine;
using static CMakeVroomifier.Cli.ConsoleHelpers;

namespace CMakeVroomifier.Cli;

public static class Program
{
    public static Task Main(string[] args)
        => Parser.Default
                 .ParseArguments<Options>(args)
                 .WithParsedAsync(RunWithOptions);

    private static async Task ExecuteAndWait(IReadOnlyCollection<string> changes, Options opts, CancellationToken cancellationToken)
    {
        Console.Clear();

        if (changes.Any())
        {
            WriteHeader("Changes detected");
            Console.WriteLine(changes.Select(f => $" - {f}").Join(Environment.NewLine));
            Console.WriteLine();
        }

        await ExecuteConfigureBuildTest(opts, cancellationToken);

        WriteHeader("Waiting for changes", ConsoleColor.DarkGray);
    }

    private static async Task ExecuteConfigureBuildTest(Options opts, CancellationToken cancellationToken)
    {
        ProcessHelpers.WaitForNoCmakeProcesses(cancellationToken);
        ProcessHelpers.WaitForNoCtestProcesses(cancellationToken);

        IEnumerable<Func<Task<bool>>> actions =
        [
            () => CMakeHelpers.ConfigureAsync(opts, cancellationToken),
            () => CMakeHelpers.BuildAsync(opts, cancellationToken),
            () => CMakeHelpers.TestAsync(opts, cancellationToken)
        ];

        foreach (var action in actions)
        {
            if (!await action())
                return;
        }
    }

    private static async Task HandleFileChangesAsync(IReadOnlyCollection<string> changes, Options opts, CancellationToken cancellationToken)
    {
        if (await IsGitRebasingAsync())
        {
            WriteHeader("Git rebase in progress, ignoring changes", ConsoleColor.Yellow);
            return;
        }

        if (changes.Any(f => Regex.IsMatch(f, "(CMakeLists.txt|.cmake|.rc|.qrc)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)))
            opts.ConfigureMode = Options.ConfigureModes.Normal;
        if (changes.Any(f => Regex.IsMatch(f, "CMakePresets.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled)))
            opts.ConfigureMode = Options.ConfigureModes.Fresh;

        await ExecuteAndWait(changes, opts, cancellationToken);
    }

    private static async Task<bool> IsGitRebasingAsync()
    {
        try
        {
            var gitDirResult = await CliWrap.Cli.Wrap("git")
                                            .WithArguments("rev-parse --git-dir")
                                            .WithValidation(CommandResultValidation.None)
                                            .WithStandardErrorPipe(PipeTarget.Null)
                                            .WithValidation(CommandResultValidation.None)
                                            .ExecuteBufferedAsync();

            if (gitDirResult.ExitCode != 0)
            {
                WriteHeader("Not a Git repository", ConsoleColor.Yellow);
                Environment.Exit(1);
            }

            var gitDir = gitDirResult.StandardOutput.Trim();
            var rebaseMerge = Path.Combine(gitDir, "rebase-merge");
            var rebaseApply = Path.Combine(gitDir, "rebase-apply");

            if (Directory.Exists(rebaseMerge) || Directory.Exists(rebaseApply))
                return true;
            return false;
        }
        catch (Exception ex)
        {
            WriteHeader($"Error checking git rebase: {ex.Message}", ConsoleColor.Red);
            Environment.Exit(1);
            return false;
        }
    }

    private static async Task<int> RunWithOptions(Options opts)
    {
        WriteHeader("options", ConsoleColor.DarkGreen);
        Console.WriteLine($"Path: {opts.Path}");
        Console.WriteLine($"ConfigurePreset: {opts.ConfigurePreset}");
        Console.WriteLine($"BuildPreset: {opts.BuildPreset}");
        Console.WriteLine($"TestPreset: {opts.TestPreset}");
        Console.WriteLine($"ExcludeTests: {opts.ExcludeTests}");
        Console.WriteLine($"Fresh: {opts.ConfigureFresh}");
        Console.WriteLine($"PreConfigureScript: {opts.PreConfigureScript}");
        Console.WriteLine($"PreBuildScript: {opts.PreBuildScript}");
        Console.WriteLine($"PreTestScript: {opts.PreTestScript}");
        Console.WriteLine($"PostTestScript: {opts.PostTestScript}");

        using var fileWatcher = new FileWatcher(opts.Path,
                                                ["*.cpp", "*.h", "*.c", "*.hpp", "*.rc", "*.qrc", "CMakeLists.txt", "*.cmake", "*.ui"],
                                                ["build/", ".vs/"]);

        BehaviorSubject<CancellationTokenSource> cts = new(new CancellationTokenSource());
        var bufferCloser = new BehaviorSubject<Unit>(Unit.Default);
        var rebuildReasons = fileWatcher.Changes
                                        .Do(_ => cts.Value.Cancel())
                                        .Buffer(bufferCloser)
                                        .Select(files => files.Distinct().ToImmutableList())
                                        .Select(f => f.Any()
                                                         ? (object)new FilesChanged(f)
                                                         : new RebuildNotRequired())
                                        .Merge(opts.RebuildReasons
                                                   .Do(_ => cts.Value.Cancel()))
                                        .ToEnumerable();

        opts.ConfigureMode = Options.ConfigureModes.Fresh;
        foreach (var rebuildReason in rebuildReasons)
        {
            IReadOnlyCollection<string> changedFiles = [];
            var doRun = opts.ConfigureMode != Options.ConfigureModes.None;

            switch (rebuildReason)
            {
                case FilesChanged(var files):
                    changedFiles = files;
                    doRun = true;
                    break;

                case FreshConfigureRequired:
                    opts.ConfigureFresh = true;
                    doRun = true;
                    break;
            }

            if (doRun)
            {
                cts.OnNext(new CancellationTokenSource());
                await HandleFileChangesAsync(changedFiles, opts, cts.Value.Token)
                    .IgnoreCancellationException();
            }

            cts.Value.Token.WaitHandle.WaitOne(Timeout.Infinite);
            bufferCloser.OnNext(Unit.Default);
        }

        return 0;
    }
}
