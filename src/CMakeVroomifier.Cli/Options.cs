using CommandLine;

namespace CMakeVroomifier.Cli
{
    public class Options
    {
        [Option('p', "path", Required = false, HelpText = "Path to the project directory.")]
        public string Path { get; set; } = Directory.GetCurrentDirectory();

        [Option("configure-preset", Required = false, HelpText = "CMake configure preset.")]
        public string? ConfigurePreset { get; set; }

        [Option("build-preset", Required = false, HelpText = "CMake build preset.")]
        public string? BuildPreset { get; set; }

        [Option("test-preset", Required = false, HelpText = "CMake test preset.")]
        public string? TestPreset { get; set; }

        [Option("exclude-tests", Required = false, HelpText = "Pattern to exclude tests.")]
        public string? ExcludeTests { get; set; }
    }
}
