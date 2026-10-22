using System.Diagnostics;
using System.Runtime.InteropServices;
using BetterMalodyV.Shared;

namespace BetterMalodyV.Plugin;

internal sealed class LocalStatusStore
{
    private ChartStatusIndex index = new();
    private int running, generation, appliedGeneration;
    private long stamp = -1;
    private string? lastError;
    internal ChartStatusIndex Index => Volatile.Read(ref index);
    internal bool Ready { get; private set; }
    internal long LastBuildMilliseconds { get; private set; }
    internal int Generation => Volatile.Read(ref generation);
    internal bool RefreshPending => Generation != Volatile.Read(ref appliedGeneration);
    internal void MarkApplied(int value) => Volatile.Write(ref appliedGeneration,value);

    internal void RequestRefresh(string gameRoot)
    {
        if (Interlocked.CompareExchange(ref running,1,0) != 0) return;
        _ = Task.Run(() =>
        {
            try
            {
                var source = Path.Combine(gameRoot,"chart.bin");
                if (!File.Exists(source)) return;
                var file = new FileInfo(source);
                var currentStamp = file.LastWriteTimeUtc.Ticks ^ file.Length;
                if (Ready && currentStamp == stamp) return;
                var watch = Stopwatch.StartNew();
                var next = ReadSnapshot(gameRoot,source);
                file.Refresh();
                if ((file.LastWriteTimeUtc.Ticks ^ file.Length) != currentStamp) return;
                LastBuildMilliseconds = watch.ElapsedMilliseconds;
                Volatile.Write(ref index,next); Ready = true; stamp = currentStamp;
                lastError = null;
                Interlocked.Increment(ref generation);
                Plugin.Current.LogIndexReady(next.RecordCount,watch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                if (lastError != ex.Message) { lastError = ex.Message; Plugin.Current.LogIndexFailure(ex); }
            }
            finally { Volatile.Write(ref running,0); }
        });
    }

    // The native library sees only a disposable COPY. It never opens, commits,
    // locks or closes the game's live database, and this code runs off the UI thread.
    private static unsafe ChartStatusIndex ReadSnapshot(string gameRoot,string source)
    {
        var copy = Path.Combine(Path.GetTempPath(),"BetterMalodyV-index-"+Guid.NewGuid().ToString("N")+".bin");
        try
        {
            using (var input = new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using (var output = new FileStream(copy,FileMode.CreateNew,FileAccess.Write)) input.CopyTo(output);
            var library = NativeLibrary.Load(Path.Combine(gameRoot,"Malody V_Data","Plugins","x86_64","LibCpp.dll"));
            try
            {
                var open = Marshal.GetDelegateForFunctionPointer<OpenDb>(NativeLibrary.GetExport(library,"UnqDB_New"));
                var close = Marshal.GetDelegateForFunctionPointer<CloseDb>(NativeLibrary.GetExport(library,"UnqDB_Delete"));
                var traverse = Marshal.GetDelegateForFunctionPointer<TraverseDb>(NativeLibrary.GetExport(library,"UnqDB_Traversal"));
                var handle = open(copy);
                if (handle == IntPtr.Zero) throw new InvalidDataException("Cannot open local metadata snapshot.");
                try
                {
                    var result = new ChartStatusIndex();
                    Exception? error = null;
                    Visit visitor = (_,data,length) =>
                    {
                        if (error is not null || length <= 0) return true;
                        try { result.AddRecord(new ReadOnlySpan<byte>(data.ToPointer(),length)); }
                        catch (Exception ex) { error = ex; }
                        return true;
                    };
                    traverse(handle,visitor); GC.KeepAlive(visitor);
                    if (error is not null) throw new InvalidDataException("Cannot read local chart metadata.",error);
                    return result;
                }
                finally { close(handle); }
            }
            finally { NativeLibrary.Free(library); }
        }
        finally { try { File.Delete(copy); } catch (IOException) { } }
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr OpenDb([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CloseDb(IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void TraverseDb(IntPtr handle,Visit callback);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.I1)] private delegate bool Visit(IntPtr key,IntPtr data,int length);
#if BETTER_MALODYV_VERIFY
    internal void SetTestIndex(ChartStatusIndex value) { Volatile.Write(ref index,value); Ready = true; }
#endif
}
