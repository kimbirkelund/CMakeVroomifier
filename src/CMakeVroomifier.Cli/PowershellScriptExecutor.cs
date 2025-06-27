using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Text;

namespace CMakeVroomifier.Cli;

public class PowerShellScriptExecutor
{
    private readonly TextWriter _debugStreamSink;
    private readonly TextWriter _errorStreamSink;
    private readonly TextWriter _informationStreamSink;
    private readonly TextWriter _outputStreamSink;

    private readonly Runspace _runspace;
    private readonly TextWriter _verboseStreamSink;
    private readonly TextWriter _warningStreamSink;

    public static PowerShellScriptExecutor Default { get; } = new(Console.Out, Console.Out, Console.Out, Console.Out, Console.Error, Console.Out, Console.Out);

    public PowerShellScriptExecutor(
        TextWriter verboseStreamSink,
        TextWriter debugStreamSink,
        TextWriter informationStreamSink,
        TextWriter warningStreamSink,
        TextWriter errorStreamSink,
        TextWriter outputStreamSink,
        TextWriter hostStreamSink)
    {
        _verboseStreamSink = verboseStreamSink;
        _debugStreamSink = debugStreamSink;
        _informationStreamSink = informationStreamSink;
        _warningStreamSink = warningStreamSink;
        _errorStreamSink = errorStreamSink;
        _outputStreamSink = outputStreamSink;

        _runspace = RunspaceFactory.CreateRunspace(new CustomPsHost(errorStreamSink, hostStreamSink));
        _runspace.Open();
    }

    /// <summary>
    /// Executes a PowerShell script provided as a base64 encoding string and returns the last boolean output.
    /// </summary>
    /// <param name="path">The directory the script should be executed in.</param>
    /// <param name="encodedScript">The PowerShell script to execute base64 encoded.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The final boolean value written to the output stream.</returns>
    public Task<bool> ExecuteEncodedScriptAsync(string path, string? encodedScript, CancellationToken cancellationToken = default)
    {
        if (encodedScript is null)
            return Task.FromResult(true);

        var script = Encoding.UTF8.GetString(Convert.FromBase64String(encodedScript));
        return ExecuteScriptAsync(path, script, cancellationToken);
    }

    /// <summary>
    /// Executes a PowerShell script provided as a string and returns the last boolean output.
    /// </summary>
    /// <param name="path">The directory the script should be executed in.</param>
    /// <param name="script">The PowerShell script to execute.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The final boolean value written to the output stream.</returns>
    public async Task<bool> ExecuteScriptAsync(string path, string? script, CancellationToken cancellationToken = default)
    {
        if (script is null)
            return true;
        if (cancellationToken.IsCancellationRequested)
            return false;

        using var powerShell = PowerShell.Create(_runspace);
        powerShell.AddScript($"Set-Location -Path {path}");
        powerShell.AddScript(script).AddParameter("CancellationToken", cancellationToken);

        SubscribeToDataCollection(powerShell.Streams.Verbose, _verboseStreamSink);
        SubscribeToDataCollection(powerShell.Streams.Debug, _debugStreamSink);
        SubscribeToDataCollection(powerShell.Streams.Information, _informationStreamSink);
        SubscribeToDataCollection(powerShell.Streams.Warning, _warningStreamSink);
        SubscribeToDataCollection(powerShell.Streams.Error, _errorStreamSink);

        try
        {
            using var inputs = new PSDataCollection<PSObject>();
            inputs.Complete();

            using PSDataCollection<PSObject> outputs = new();

            var cmdResult = powerShell.InvokeAsync(inputs, outputs);

            var finalResult = true;
            outputs.DataAdded += (_, args) =>
                                 {
                                     // ReSharper disable once AccessToDisposedClosure
                                     // It won't be disposed before completed
                                     if (outputs[args.Index] is { } output)
                                     {
                                         if (output.BaseObject is bool b)
                                             finalResult = b;
                                         else if (output.ToString() is { } outputAsString)
                                             _outputStreamSink.WriteLine(outputAsString);
                                     }
                                 };

            await cmdResult;

            if (cancellationToken.IsCancellationRequested)
                return false;
            return finalResult;
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

file class CustomPsHost(
    TextWriter errorStreamSink,
    TextWriter hostStreamSink) : PSHost
{
    public override CultureInfo CurrentCulture { get; } = CultureInfo.InvariantCulture;
    public override CultureInfo CurrentUICulture { get; } = CultureInfo.InvariantCulture;
    public override Guid InstanceId { get; } = Guid.NewGuid();

    public override string Name => "CMakeVroomifier";
    public override PSHostUserInterface UI { get; } = new CustomPsHostUserInterface(errorStreamSink, hostStreamSink);
    public override Version Version { get; } = typeof(CustomPsHost).Assembly.GetName().Version ?? new Version(0, 0);

    public int? ExitCode { get; private set; }

    public override void EnterNestedPrompt() { }

    public override void ExitNestedPrompt() { }

    public override void NotifyBeginApplication() { }

    public override void NotifyEndApplication() { }

    public override void SetShouldExit(int exitCode)
    {
        ExitCode = exitCode;
    }
}

file class CustomPsHostUserInterface(
    TextWriter errorStreamSink,
    TextWriter hostStreamSink) : PSHostUserInterface
{
    public override PSHostRawUserInterface? RawUI => null;

    public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        => new();

    public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        => 0;

    public override PSCredential? PromptForCredential(string caption, string message, string userName, string targetName)
        => null;

    public override PSCredential? PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        => null;

    public override string ReadLine()
        => "";

    public override SecureString ReadLineAsSecureString()
        => new();

    public override void Write(string value)
        => hostStreamSink.Write(value);

    public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        => Write(value);

    public override void WriteDebugLine(string message) { }

    public override void WriteErrorLine(string value)
        => errorStreamSink.WriteLine(value);

    public override void WriteLine(string value)
        => hostStreamSink.WriteLine(value);

    public override void WriteProgress(long sourceId, ProgressRecord record) { }

    public override void WriteVerboseLine(string message) { }

    public override void WriteWarningLine(string message) { }
}
