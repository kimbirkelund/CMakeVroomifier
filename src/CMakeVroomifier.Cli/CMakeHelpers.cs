using System.Xml;
using CliWrap;
using CliWrap.Buffered;

namespace CMakeVroomifier.Cli;

internal static class CMakeHelpers
{
    public static async Task<bool> BuildAsync(Options opts, CancellationToken cancellationToken)
    {
        Console.Clear();
        ConsoleHelpers.WriteHeader("Building", ConsoleColor.DarkGray);
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
                                                                                       ConsoleHelpers.WriteHeader("BUILD FAILED: fresh configuration required", ConsoleColor.Red);
                                                                                       opts.RebuildReasons.OnNext(new FreshConfigureRequired());
                                                                                       throw new OperationCanceledException("Cancel current run to trigger fresh configure.");
                                                                                   }

                                                                                   Console.Error.WriteLine(l);
                                                                               }))
                                  .WithWorkingDirectory(opts.Path);
            var result = await buildCmd.ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode == 0)
                return true;

            ConsoleHelpers.WriteHeader("BUILD FAILED", ConsoleColor.Red);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ConsoleHelpers.WriteHeader($"BUILD FAILED: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    public static async Task<bool> ConfigureAsync(Options opts, CancellationToken cancellationToken)
    {
        Console.Clear();
        ConsoleHelpers.WriteHeader("Configuring", ConsoleColor.DarkGray);
        try
        {
            var runFresh = opts.ConfigureFresh ? " --fresh" : "";
            opts.ConfigureFresh = false;

            var configureCmd = CliWrap.Cli.Wrap("cmake")
                                      .WithArguments($"--preset {opts.ConfigurePreset}{runFresh}")
                                      .WithValidation(CommandResultValidation.None)
                                      .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                                      .WithStandardErrorPipe(PipeTarget.ToDelegate(l => Console.Error.WriteLine(l)))
                                      .WithWorkingDirectory(opts.Path);
            var result = await configureCmd.ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode == 0)
                return true;

            ConsoleHelpers.WriteHeader("CONFIGURE FAILED", ConsoleColor.Red);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ConsoleHelpers.WriteHeader($"CONFIGURE FAILED: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    public static async Task<bool> TestAsync(Options opts, CancellationToken cancellationToken)
    {
        Console.Clear();
        ConsoleHelpers.WriteHeader("Testing", ConsoleColor.DarkGray);
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
                return true;

            // Parse XML and print failed tests
            Console.Clear();
            ConsoleHelpers.WriteHeader("TESTS FAILED", ConsoleColor.Red);
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
            ConsoleHelpers.WriteHeader($"TESTS FAILED: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }
}
