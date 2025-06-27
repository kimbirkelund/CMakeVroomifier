using System.Text;

namespace CMakeVroomifier.Cli.Tests;

public class PowerShellExecutorTests
{
    [Fact]
    public async Task Should_capture_all_powershell_output_streams()
    {
        // Arrange
        var outputStreamSink = new StringWriter();
        var errorStreamSink = new StringWriter();
        var warningStreamSink = new StringWriter();
        var verboseStreamSink = new StringWriter();
        var debugStreamSink = new StringWriter();
        var informationStreamSink = new StringWriter();
        var hostStreamSink = new StringWriter();

        var sut = new PowerShellScriptExecutor(
            verboseStreamSink: verboseStreamSink,
            debugStreamSink: debugStreamSink,
            informationStreamSink: informationStreamSink,
            warningStreamSink: warningStreamSink,
            errorStreamSink: errorStreamSink,
            outputStreamSink: outputStreamSink,
            hostStreamSink: hostStreamSink
        );

        var script = """
                     $VerbosePreference = "Continue";
                     $DebugPreference = "Continue";

                     Write-Output 'output message';
                     Write-Error 'error message';
                     Write-Warning 'warning message';
                     Write-Verbose 'verbose message';
                     Write-Debug 'debug message';
                     Write-Information 'information message';
                     'host message' | Out-Host
                     """;

        // Act
        await sut.ExecuteScriptAsync(Environment.CurrentDirectory, script, TestContext.Current.CancellationToken);

        // Assert
        outputStreamSink.ToString().ShouldBe("output message" + Environment.NewLine);
        errorStreamSink.ToString().ShouldBe("error message" + Environment.NewLine);
        warningStreamSink.ToString().ShouldBe("warning message" + Environment.NewLine);
        informationStreamSink.ToString().ShouldBe("information message" + Environment.NewLine);
        verboseStreamSink.ToString().ShouldBe("verbose message" + Environment.NewLine);
        debugStreamSink.ToString().ShouldBe("debug message" + Environment.NewLine);
        hostStreamSink.ToString().ShouldBe("host message" + Environment.NewLine);
    }

    [Fact]
    public async Task Should_execute_script_and_return_true_when_no_explicitly_boolean_is_outputted()
    {
        // Arrange
        var sut = PowerShellScriptExecutor.Default;

        var script = """
                     Write-Output 'Hello, World!';
                     """;

        // Act
        var result = await sut.ExecuteScriptAsync(Environment.CurrentDirectory, script, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_execute_the_encoded_script()
    {
        // Arrange
        var sut = PowerShellScriptExecutor.Default;

        var script = """
                     Write-Output 'Hello, World!';
                     return $false;
                     """;
        var encodedScript = Convert.ToBase64String(Encoding.UTF8.GetBytes(script));

        // Act
        var result = await sut.ExecuteEncodedScriptAsync(Environment.CurrentDirectory, encodedScript, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_not_capture_silenced_output()
    {
        // Arrange
        var outputStreamSink = new StringWriter();
        var errorStreamSink = new StringWriter();
        var warningStreamSink = new StringWriter();
        var verboseStreamSink = new StringWriter();
        var debugStreamSink = new StringWriter();
        var informationStreamSink = new StringWriter();
        var hostStreamSink = new StringWriter();

        var sut = new PowerShellScriptExecutor(
            verboseStreamSink: verboseStreamSink,
            debugStreamSink: debugStreamSink,
            informationStreamSink: informationStreamSink,
            warningStreamSink: warningStreamSink,
            errorStreamSink: errorStreamSink,
            outputStreamSink: outputStreamSink,
            hostStreamSink: hostStreamSink
        );

        var script = """
                     $ErrorActionPreference = "Ignore";
                     $WarningPreference = "Ignore"
                     $InformationPreference = "Ignore";
                     $VerbosePreference = "Ignore";
                     $DebugPreference = "Ignore";

                     Write-Error 'error message';
                     Write-Warning 'warning message';
                     Write-Verbose 'verbose message';
                     Write-Debug 'debug message';
                     Write-Information 'information message';
                     """;

        // Act
        await sut.ExecuteScriptAsync(Environment.CurrentDirectory, script, TestContext.Current.CancellationToken);

        // Assert
        errorStreamSink.ToString().ShouldBeEmpty();
        warningStreamSink.ToString().ShouldBeEmpty();
        informationStreamSink.ToString().ShouldBeEmpty();
        verboseStreamSink.ToString().ShouldBeEmpty();
        debugStreamSink.ToString().ShouldBeEmpty();
        hostStreamSink.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_return_the_bool_value_returned_from_the_script()
    {
        // Arrange
        var sut = PowerShellScriptExecutor.Default;

        var script = """
                     Write-Output 'Hello, World!';
                     return $false;
                     """;

        // Act
        var result = await sut.ExecuteScriptAsync(Environment.CurrentDirectory, script, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }
}
