using System.Reflection;

namespace BetterMalodyV.Plugin;

internal sealed class NativePending
{
    private readonly object awaiter;
    private readonly MethodInfo completed;
    private readonly MethodInfo result;
    internal object? Result { get; private set; }
    internal NativePending(object task)
    {
        awaiter=task.GetType().GetMethod("GetAwaiter")!.Invoke(task,null)!;
        completed=awaiter.GetType().GetMethod("get_IsCompleted")??throw new NotSupportedException("Native awaiter completion API changed.");
        result=awaiter.GetType().GetMethod("GetResult")!;
    }
    internal bool Poll()
    {
        if(!(bool)completed.Invoke(awaiter,null)!)return false;
        Result=result.Invoke(awaiter,null);return true;
    }
}
