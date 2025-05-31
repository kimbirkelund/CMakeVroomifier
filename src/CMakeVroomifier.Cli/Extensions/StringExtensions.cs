namespace CMakeVroomifier.Cli;

public static class StringExtensions
{
    public static string Join(this ReadOnlySpan<object?> source, string? separator)
        => string.Join(separator, source);

    public static string Join(this IEnumerable<object?>? source, string? separator)
        => string.Join(separator, source ?? []);
}
