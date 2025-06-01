using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Xml;
using CliWrap;
using CliWrap.Buffered;
using CMakeVroomifier.Cli;
using CommandLine;
using static CMakeVroomifier.Cli.ConsoleHelpers;

await Parser.Default
            .ParseArguments<Options>(args)
            .WithParsedAsync(RunWithOptions);

static async Task<int> RunWithOptions(Options opts)
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

    var firstRun = true;
    foreach (var changedFiles in changes)
    {
        if (changedFiles.Any() || firstRun)
        {
            cts.OnNext(new CancellationTokenSource());
            await HandleFileChangesAsync(changedFiles, firstRun, opts, cts.Value.Token);
            firstRun = false;
        }

        cts.Value.Token.WaitHandle.WaitOne(Timeout.Infinite);
        bufferCloser.OnNext(Unit.Default);
    }

    return 0;
}

static async Task HandleFileChangesAsync(IList<string> changes, bool runConfigure, Options opts, CancellationToken cancellationToken)
{
    if (await IsGitRebasingAsync())
    {
        WriteHeader("Git rebase in progress, ignoring changes", ConsoleColor.Yellow);
        return;
    }

    runConfigure |= changes.Any(f => Regex.IsMatch(f, "(CMakeLists.txt|.cmake|.rc|.qrc)$", RegexOptions.IgnoreCase | RegexOptions.Compiled));
    await ExecuteAndWait(runConfigure, changes, opts, cancellationToken);
}

static async Task ExecuteAndWait(bool runConfigure, IList<string> changes, Options opts, CancellationToken cancellationToken)
{
    Console.Clear();

    if (changes.Any())
    {
        WriteHeader("Changes detected");
        Console.WriteLine(changes.Select(f => $" - {f}").Join(Environment.NewLine));
        Console.WriteLine();
    }

    await ExecuteConfigureBuildTest(runConfigure, opts, cancellationToken);

    WriteHeader("Waiting for changes", ConsoleColor.DarkGray);
}

static async Task ExecuteConfigureBuildTest(bool runConfigure, Options opts, CancellationToken cancellationToken)
{
    WaitForNoCmakeProcesses(cancellationToken);
    WaitForNoCtestProcesses(cancellationToken);

    if (runConfigure)
    {
        Console.Clear();
        WriteHeader("Configuring", ConsoleColor.DarkGray);
        try
        {
            var configureCmd = Cli.Wrap("cmake")
                                  .WithArguments($"--preset {opts.ConfigurePreset}")
                                  .WithValidation(CommandResultValidation.None)
                                  .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                                  .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Console.Error.WriteLine(s)))
                                  .WithWorkingDirectory(opts.Path);
            var result = await configureCmd.ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode != 0)
            {
                WriteHeader("CONFIGURE FAILED", ConsoleColor.Red);
                return;
            }
        }
        catch (Exception ex)
        {
            WriteHeader($"CONFIGURE FAILED: {ex.Message}", ConsoleColor.Red);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;
    }

    Console.Clear();
    WriteHeader("Building", ConsoleColor.DarkGray);
    try
    {
        var buildCmd = Cli.Wrap("cmake")
                          .WithArguments($"--build --preset {opts.BuildPreset} -j 20")
                          .WithValidation(CommandResultValidation.None)
                          .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                          .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Console.Error.WriteLine(s)))
                          .WithWorkingDirectory(opts.Path);
        var result = await buildCmd.ExecuteBufferedAsync(cancellationToken);
        if (result.ExitCode != 0)
        {
            WriteHeader("BUILD FAILED", ConsoleColor.Red);
            return;
        }
    }
    catch (Exception ex)
    {
        WriteHeader($"BUILD FAILED: {ex.Message}", ConsoleColor.Red);
        return;
    }

    if (cancellationToken.IsCancellationRequested)
        return;

    Console.Clear();
    WriteHeader("Testing", ConsoleColor.DarkGray);
    var xmlOutput = Path.GetTempFileName();
    var testArgs = $"--preset {opts.TestPreset} --parallel --progress --output-junit {xmlOutput}";
    if (!string.IsNullOrWhiteSpace(opts.ExcludeTests))
        testArgs += $" --exclude-regex {opts.ExcludeTests}";
    try
    {
        var testCmd = Cli.Wrap("ctest")
                         .WithArguments(testArgs)
                         .WithValidation(CommandResultValidation.None)
                         .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                         .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Console.Error.WriteLine(s)))
                         .WithWorkingDirectory(opts.Path);
        var result = await testCmd.ExecuteBufferedAsync(cancellationToken);
        if (result.ExitCode != 0)
        {
            // Parse XML and print failed tests
            Console.Clear();
            WriteHeader("TESTS FAILED", ConsoleColor.Red);
            try
            {
                var xml = new XmlDocument();
                xml.Load(xmlOutput);
                var testCases = xml.SelectNodes("//testcase[@status='fail']");
                if (testCases != null)
                {
                    foreach (XmlNode testCase in testCases)
                    {
                        var name = testCase?.Attributes?["name"]?.Value;
                        var h = $"### {name}";
                        Console.WriteLine(new string('#', h.Length));
                        Console.WriteLine(h);
                        Console.WriteLine();
                        var systemOut = testCase?.SelectSingleNode("system-out")?.InnerText;
                        if (!string.IsNullOrEmpty(systemOut))
                            Console.WriteLine(systemOut);
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing test results: {ex.Message}");
            }

            return;
        }
    }
    catch (Exception ex)
    {
        WriteHeader($"TESTS FAILED: {ex.Message}", ConsoleColor.Red);
        return;
    }

    if (cancellationToken.IsCancellationRequested)
        return;

    Console.Clear();
    WriteHeader("All tests passed.", ConsoleColor.Green);
}

static async Task<bool> IsGitRebasingAsync()
{
    try
    {
        var gitDirResult = await Cli.Wrap("git")
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

static void WaitForNoCmakeProcesses(CancellationToken cancellationToken)
{
    var processName = "cmake";
    var processes = Process.GetProcessesByName(processName);
    if (processes.Length == 0)
        return;

    Console.Write("Waiting for cmake processes to finish...");
    while (processes.Length > 0 && !cancellationToken.IsCancellationRequested)
    {
        Console.Write(".");
        Thread.Sleep(1000);
        processes = Process.GetProcessesByName(processName);
    }

    Console.WriteLine();
}

static void WaitForNoCtestProcesses(CancellationToken cancellationToken)
{
    var processName = "ctest";
    var processes = Process.GetProcessesByName(processName);
    if (processes.Length == 0)
        return;

    Console.Write("Waiting for ctest processes to finish...");
    while (processes.Length > 0 && !cancellationToken.IsCancellationRequested)
    {
        Console.Write(".");
        Thread.Sleep(1000);
        processes = Process.GetProcessesByName(processName);
    }

    Console.WriteLine();
}
