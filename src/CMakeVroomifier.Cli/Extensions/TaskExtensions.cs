namespace CMakeVroomifier.Cli;

public static class TaskExtensions
{
    public static async Task IgnoreCancellationException<T>(this Task<T> task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
    }

    public static async Task<T> IgnoreCancellationException<T>(this Task<T> task, Func<T> valueOnCancelled)
    {
        try
        {
            return await task;
        }
        catch (OperationCanceledException)
        {
            return valueOnCancelled();
        }
    }

    public static Task<T> IgnoreCancellationException<T>(this Task<T> task, T valueOnCancelled)
        => IgnoreCancellationException(task, () => valueOnCancelled);

    public static async Task IgnoreCancellationException(this Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
    }
}
