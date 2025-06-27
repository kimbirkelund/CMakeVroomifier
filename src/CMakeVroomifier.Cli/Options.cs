using System.Reactive.Subjects;
using CommandLine;

namespace CMakeVroomifier.Cli;

public class Options
{
    public ISubject<object> RebuildReasons { get; } = new Subject<object>();

    [Option("build-preset",
            Required = true,
            SetName = "IndividualPresets",
            HelpText = "CMake build preset.")]
    public string? BuildPreset
    {
        get => field ?? Preset;
        set;
    }

    [Option("configure-fresh",
            Required = false,
            HelpText = "Run the first CMake configure with the '--fresh' switch.")]
    public bool ConfigureFresh
    {
        get => ConfigureMode is ConfigureModes.Fresh;
        set
        {
            if (value)
                ConfigureMode = ConfigureModes.Fresh;
            else if (!value && ConfigureMode is ConfigureModes.Fresh)
                ConfigureMode = ConfigureModes.None;
        }
    }

    public ConfigureModes ConfigureMode { get; set; }

    [Option("configure-preset",
            Required = true,
            SetName = "IndividualPresets",
            HelpText = "CMake configure preset.")]
    public string? ConfigurePreset
    {
        get => field ?? Preset;
        set;
    }

    [Option("exclude-tests",
            Required = false,
            HelpText = "Pattern to exclude tests.")]
    public string? ExcludeTests { get; set; }

    [Option('p',
            "path",
            Required = false,
            HelpText = "Path to the project directory.")]
    public string Path { get; set; } = Directory.GetCurrentDirectory();

    [Option("post-test-script",
            Required = false,
            HelpText = "Powershell script that will be run after CMake tests.")]
    public string? PostTestScript { get; set; }

    [Option("post-test-script-encoded",
            Required = false,
            HelpText = "Powershell script base64 encoded that will be run after CMake tests.")]
    public string? PostTestScriptEncoded { get; set; }

    [Option("pre-build-script",
            Required = false,
            HelpText = "Powershell script that will be run before CMake build.")]
    public string? PreBuildScript { get; set; }

    [Option("pre-build-script-encoded",
            Required = false,
            HelpText = "Powershell script base64 encoded that will be run before CMake build.")]
    public string? PreBuildScriptEncoded { get; set; }

    [Option("pre-configure-script",
            Required = false,
            HelpText = "Powershell script that will be run before CMake configure.")]
    public string? PreConfigureScript { get; set; }

    [Option("pre-configure-script-encoded",
            Required = false,
            HelpText = "Powershell script base64 encoded that will be run before CMake configure.")]
    public string? PreConfigureScriptEncoded { get; set; }

    [Option('s',
            "preset",
            SetName = "SharedPreset",
            Required = true,
            HelpText = "CMake preset used for configure, build and test.")]
    public string? Preset { get; set; }

    [Option("pre-test-script",
            Required = false,
            HelpText = "Powershell script that will be run before CMake tests.")]
    public string? PreTestScript { get; set; }

    [Option("pre-test-script-encoded",
            Required = false,
            HelpText = "Powershell script base64 encoded that will be run before CMake tests.")]
    public string? PreTestScriptEncoded { get; set; }

    [Option("test-preset",
            SetName = "IndividualPresets",
            Required = true,
            HelpText = "CMake test preset.")]
    public string? TestPreset
    {
        get => field ?? Preset;
        set;
    }

    public enum ConfigureModes
    {
        None,
        Normal,
        Fresh
    }
}
