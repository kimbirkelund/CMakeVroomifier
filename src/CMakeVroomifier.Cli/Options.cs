using CommandLine;

namespace CMakeVroomifier.Cli;

public class Options
{
    [Option("build-preset",
            Required = true,
            SetName = "IndividualPresets",
            HelpText = "CMake build preset.")]
    public string? BuildPreset
    {
        get => field ?? Preset;
        set;
    }

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

    [Option('s',
            "preset",
            SetName = "SharedPreset",
            Required = true,
            HelpText = "CMake preset used for configure, build and test.")]
    public string? Preset { get; set; }

    [Option("test-preset",
            SetName = "IndividualPresets",
            Required = true,
            HelpText = "CMake test preset.")]
    public string? TestPreset
    {
        get => field ?? Preset;
        set;
    }
}
