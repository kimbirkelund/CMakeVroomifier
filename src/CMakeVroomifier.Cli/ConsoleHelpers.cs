namespace CMakeVroomifier.Cli;

public static class ConsoleHelpers
{
    private static int ConsoleWindowWidth
    {
        get
        {
            try
            {
                return Console.WindowWidth;
            }
            catch
            {
                return 80;
            }
        }
    }

    public static void WriteHeader(string text, ConsoleColor? foregroundColor = null)
    {
        var prevColor = Console.ForegroundColor;
        if (foregroundColor.HasValue)
            Console.ForegroundColor = foregroundColor.Value;

        var width = ConsoleWindowWidth;

        width = Math.Max(width, text.Length + 12); // Ensure enough space for header

        Console.WriteLine(new string('#', width));
        var header = $"#####  {text}  ";
        if (header.Length < width)
            header = header.PadRight(width, '#');
        Console.WriteLine(header);
        Console.WriteLine(new string('#', width));
        Console.WriteLine();

        if (foregroundColor.HasValue)
            Console.ForegroundColor = prevColor;
    }
}
