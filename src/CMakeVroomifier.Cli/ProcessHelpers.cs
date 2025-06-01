using System.Diagnostics;

namespace CMakeVroomifier.Cli;

public static class ProcessHelpers
{
    public static void WaitForNoCmakeProcesses(CancellationToken cancellationToken)
    {
        var processName = "cmake";
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
            return;

        Console.Write("Waiting for cmake processes to finish...");
        while (processes.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            Console.Write(".");
            Thread.Sleep(1000);
            processes = Process.GetProcessesByName(processName);
        }

        Console.WriteLine();
    }

    public static void WaitForNoCtestProcesses(CancellationToken cancellationToken)
    {
        var processName = "ctest";
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
            return;

        Console.Write("Waiting for ctest processes to finish...");
        while (processes.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            Console.Write(".");
            Thread.Sleep(1000);
            processes = Process.GetProcessesByName(processName);
        }

        Console.WriteLine();
    }
}