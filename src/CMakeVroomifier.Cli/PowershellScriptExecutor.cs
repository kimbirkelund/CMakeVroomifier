using System.Management.Automation;
using System.Text;

namespace CMakeVroomifier.Cli;

public static class PowershellScriptExecutor
{
    private static readonly PowerShell _powerShell = PowerShell.Create();

    public static Task<bool> ExecuteEncodedScriptAsync(string? encodedScript, CancellationToken cancellationToken = default)
    {
        if (encodedScript is null)
            return Task.FromResult(true);

        var script = Encoding.UTF8.GetString(Convert.FromBase64String(encodedScript));
        return ExecuteScriptAsync(script, cancellationToken);
    }

    /// <summary>
    /// Executes a PowerShell script provided as a string and returns the output as a string.
    /// </summary>
    /// <param name="script">The PowerShell script to execute.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The combined output (stdout and stderr) of the script.</returns>
    public static async Task<bool> ExecuteScriptAsync(string? script, CancellationToken cancellationToken = default)
    {
        if (script is null)
            return true;
        if (cancellationToken.IsCancellationRequested)
            return false;


        _powerShell.AddScript(script);

        SubscribeToDataCollection(_powerShell.Streams.Verbose, Console.Out);
        SubscribeToDataCollection(_powerShell.Streams.Debug, Console.Out);
        SubscribeToDataCollection(_powerShell.Streams.Information, Console.Out);
        SubscribeToDataCollection(_powerShell.Streams.Warning, Console.Out);
        SubscribeToDataCollection(_powerShell.Streams.Error, Console.Error);

        try
        {
            var results = await _powerShell.InvokeAsync();

            if (cancellationToken.IsCancellationRequested)
                return false;
            if (!results.Any())
                return true;
            if (results.Where(r => r?.BaseObject is bool)
                       .Select(r => r.BaseObject is bool b ? b : (bool?)null)
                       .LastOrDefault() is { } result)
                return result;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }

        return !cancellationToken.IsCancellationRequested;
    }

    private static void SubscribeToDataCollection<T>(PSDataCollection<T> dataCollection, TextWriter textWriter)
    {
        dataCollection.DataAdded += (_, args) =>
                                    {
                                        if (dataCollection[args.Index] is { } record)
                                            textWriter.WriteLine(record.ToString());
                                    };
    }
}
