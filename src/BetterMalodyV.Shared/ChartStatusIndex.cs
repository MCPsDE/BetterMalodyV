using System.Text;

namespace BetterMalodyV.Shared;

/// <summary>Immutable after publication. A query touches only cached folder summaries.</summary>
public sealed class ChartStatusIndex
{
    private readonly Dictionary<string, uint> folders = new(StringComparer.OrdinalIgnoreCase);
    public int RecordCount { get; private set; }
    public int FolderCount => folders.Count;
    public void Add(string chartPath,uint fileMode,uint rankStatus)
    {
        RecordCount++;
        if (rankStatus > 2 || string.IsNullOrEmpty(chartPath)) return;
        var mode = ToPlayMode(fileMode);
        if (mode >= 10) return;
        var bits = 1u << (mode*3+(int)rankStatus);
        var path = Normalize(chartPath);
        for (var end = path.LastIndexOf('/'); end > 0; end = path.LastIndexOf('/',end-1))
        {
            var folder = path[..end];
            folders.TryGetValue(folder,out var previous); folders[folder] = previous|bits;
        }
    }
    public bool Matches(string songPath,int mode,StatusMask mask)
    {
        if (mask == StatusMask.All) return true;
        if (mask == StatusMask.None || !folders.TryGetValue(Normalize(songPath),out var bits)) return false;
        if (mode < 10 && mode >= 0) return ((bits >> (mode*3)) & (uint)mask) != 0;
        for (var i = 0; i < 10; i++) if (((bits >> (i*3)) & (uint)mask) != 0) return true;
        return false;
    }
    private static string Normalize(string path) => path.Replace('\\','/').TrimEnd('/');

    // Source FileModeToPlayMode order matters when a format carries multiple flags.
    public static int ToPlayMode(uint value) => (value & 0x30071) != 0 ? 0
        : (value & 0x8A) != 0 ? 3 : (value & 0x100) != 0 ? 4
        : (value & 0x604) != 0 ? 5 : (value & 0x800) != 0 ? 6
        : (value & 0x3000) != 0 ? 7 : (value & 0x4000) != 0 ? 8
        : (value & 0x8000) != 0 ? 9 : 10;

    /// <summary>Read only Path(7), ChartMode(15), RankStatus(16) from the game's
    /// protobuf record; no game/Unity calls and no chart file parsing.</summary>
    public void AddRecord(ReadOnlySpan<byte> data)
    {
        string path = ""; uint mode = 0, status = 0;
        var position = 0;
        while (position < data.Length)
        {
            var tag = ReadVarint(data,ref position);
            var field = tag >> 3;
            if (field == 0) throw new InvalidDataException("Invalid chart metadata field.");
            switch (tag & 7)
            {
                case 0:
                    var number = ReadVarint(data,ref position);
                    if (field == 15) mode = checked((uint)number);
                    else if (field == 16) status = checked((uint)number);
                    break;
                case 1: Advance(data,ref position,8); break;
                case 2:
                    var length = checked((int)ReadVarint(data,ref position));
                    var start = position; Advance(data,ref position,length);
                    if (field == 7) path = Encoding.UTF8.GetString(data.Slice(start,length));
                    break;
                case 5: Advance(data,ref position,4); break;
                default: throw new InvalidDataException("Unsupported chart metadata wire type.");
            }
        }
        Add(path,mode,status);
    }
    private static ulong ReadVarint(ReadOnlySpan<byte> data,ref int position)
    {
        ulong value = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            if (position >= data.Length) throw new InvalidDataException("Truncated chart metadata.");
            var next = data[position++];
            if (shift == 63 && next > 1) throw new InvalidDataException("Invalid metadata integer.");
            value |= (ulong)(next & 127) << shift;
            if (next < 128) return value;
        }
        throw new InvalidDataException("Invalid metadata integer.");
    }
    private static void Advance(ReadOnlySpan<byte> data,ref int position,int length)
    {
        if (length < 0 || length > data.Length-position) throw new InvalidDataException("Truncated chart metadata value.");
        position += length;
    }
}
