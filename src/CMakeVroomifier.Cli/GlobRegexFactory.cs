using System.Text.RegularExpressions;

namespace CMakeVroomifier.Cli;

public static class GlobRegexFactory
{
    public static Regex Create(string pattern)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        var directorySeparator = Path.DirectorySeparatorChar.ToString();

        pattern = pattern.Replace("/", directorySeparator)
                         .Replace("\\", directorySeparator);

        var escapedDirectorySeparator = Regex.Escape(directorySeparator);
        var regexRootOrFolder = $"(^|{escapedDirectorySeparator})";

        // Match *.ext
        var extMatch = Regex.Match(pattern, @"^\*\.(?<extension>[a-zA-Z0-9_-]+)$");
        if (extMatch.Success)
        {
            var extension = extMatch.Groups["extension"].Value;
            var regexDotExtension = $".*\\.{extension}$";
            return new Regex($"{regexRootOrFolder}{regexDotExtension}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        // Match simple file name (no sep, no *)
        if (!pattern.Contains(directorySeparator) && !pattern.Contains("*") && !pattern.Contains("?"))
            return new Regex($"{regexRootOrFolder}{Regex.Escape(pattern)}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // General glob
        var regexPattern = Regex.Escape(pattern)
                                .Replace(@"\*", ".*")
                                .Replace(@"\?", ".");
        regexPattern = $"^{regexPattern}$";

        if (regexPattern.EndsWith($"{escapedDirectorySeparator}$"))
            regexPattern = $"{regexPattern[..^(escapedDirectorySeparator.Length + 1)]}({escapedDirectorySeparator}.*$|$)";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
