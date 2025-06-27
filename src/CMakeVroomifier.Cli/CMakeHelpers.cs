using System.Xml;
using CliWrap;
using CliWrap.Buffered;
using static CMakeVroomifier.Cli.ConsoleHelpers;

namespace CMakeVroomifier.Cli;

internal static class CMakeHelpers
{
    public static async Task<bool> BuildAsync(Options opts, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        Console.Clear();

        if (!await RunScriptsAsync("Pre-building script", opts.Path, opts.PreBuildScript, opts.PreBuildScriptEncoded, cancellationToken))
            return false;

        WriteHeader("Building", ConsoleColor.DarkGray);
        try
        {
            var buildCmd = CliWrap.Cli.Wrap("cmake")
                                  .WithArguments($"--build --preset {opts.BuildPreset} -j 20")
                                  .WithValidation(CommandResultValidation.None)
                                  .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                                  .WithStandardErrorPipe(PipeTarget.ToDelegate(l =>
                                                                               {
                                                                                   if (l.Contains("edge && !edge->outputs_ready()"))
                                                                                   {
                                                                                       WriteHeader("BUILD FAILED: fresh configuration required", ConsoleColor.Red);
                                                                                       opts.RebuildReasons.OnNext(new FreshConfigureRequired());
                                                                                       throw new OperationCanceledException("Cancel current run to trigger fresh configure.");
                                                                                   }

                                                                                   Console.Error.WriteLine(l);
                                                                               }))
                                  .WithWorkingDirectory(opts.Path);
            var result = await buildCmd.ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode == 0)
                return true;

            WriteHeader("BUILD FAILED", ConsoleColor.Red);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            WriteHeader($"BUILD FAILED: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    public static async Task<bool> ConfigureAsync(Options opts, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;
        if (opts.ConfigureMode is Options.ConfigureModes.None)
            return true;

        Console.Clear();

        if (!await RunScriptsAsync("Pre-configure script", opts.Path, opts.PreConfigureScript, opts.PreConfigureScriptEncoded, cancellationToken))
            return false;

        WriteHeader("Configuring", ConsoleColor.DarkGray);
        try
        {
            var runFresh = opts.ConfigureFresh ? " --fresh" : "";
            opts.ConfigureMode = Options.ConfigureModes.None;

            var configureCmd = CliWrap.Cli.Wrap("cmake")
                                      .WithArguments($"--preset {opts.ConfigurePreset}{runFresh}")
                                      .WithValidation(CommandResultValidation.None)
                                      .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                                      .WithStandardErrorPipe(PipeTarget.ToDelegate(l => Console.Error.WriteLine(l)))
                                      .WithWorkingDirectory(opts.Path);
            var result = await configureCmd.ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode == 0)
                return true;

            WriteHeader("CONFIGURE FAILED", ConsoleColor.Red);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            WriteHeader($"CONFIGURE FAILED: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    public static async Task<bool> TestAsync(Options opts, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        Console.Clear();

        if (!await RunScriptsAsync("Pre-test script", opts.Path, opts.PreTestScript, opts.PreTestScriptEncoded, cancellationToken))
            return false;

        WriteHeader("Testing", ConsoleColor.DarkGray);
        var xmlOutput = Path.GetTempFileName();
        var testArgs = $"--preset {opts.TestPreset} --parallel --progress --output-junit {xmlOutput}";
        if (!string.IsNullOrWhiteSpace(opts.ExcludeTests))
            testArgs += $" --exclude-regex {opts.ExcludeTests}";
        try
        {
            var testCmd = CliWrap.Cli.Wrap("ctest")
                                 .WithArguments(testArgs)
                                 .WithValidation(CommandResultValidation.None)
                                 .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                                 .WithStandardErrorPipe(PipeTarget.ToDelegate(l => Console.Error.WriteLine(l)))
                                 .WithWorkingDirectory(opts.Path);
            var result = await testCmd.ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode == 0)
            {
                Console.Clear();
                WriteHeader("All tests passed.", ConsoleColor.Green);

                if (!await RunScriptsAsync("Post-test script", opts.Path, opts.PostTestScript, opts.PostTestScriptEncoded, cancellationToken))
                    return false;

                return true;
            }

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
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing test results: {ex.Message}");
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            WriteHeader($"TESTS FAILED: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    private static async Task<bool> RunScriptsAsync(string header, string path, string? script, string? encodedScript, CancellationToken cancellationToken)
    {
        if (script is { Length: > 0 } || encodedScript is { Length: > 0 })
        {
            WriteHeader(header, ConsoleColor.DarkGray);

            if (!await PowerShellScriptExecutor.Default.ExecuteScriptAsync(path, script, cancellationToken))
                return false;
            if (!await PowerShellScriptExecutor.Default.ExecuteEncodedScriptAsync(path, encodedScript, cancellationToken))
                return false;
        }

        return true;
    }
}
