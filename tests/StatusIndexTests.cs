using System.Diagnostics;
using System.Text;
using BetterMalodyV.Shared;

internal static class StatusIndexTests
{
    internal static void Run()
    {
        var assertions = 0;
        void Check(bool value,string message) { if (!value) throw new Exception(message); assertions++; }
        var index = new ChartStatusIndex();
        index.AddRecord(Record("E:/曲库/Song/stable.mc",0x40,2));
        index.AddRecord(Record("E:/曲库/Song/beta.mc",0x200,1));
        index.AddRecord(Record("E:/曲库/Alpha/child/alpha.mc",0x40,0));
        index.AddRecord(Record("E:/曲库/Unknown/chart.mc",0x40,10));
        Check(index.Matches("e:\\曲库\\song\\",0,StatusMask.Stable),"Canonical Windows path");
        Check(!index.Matches("E:/曲库/Song",5,StatusMask.Stable),"Same chart must match status and mode");
        Check(index.Matches("E:/曲库/Song",5,StatusMask.Beta),"Taiko Beta");
        Check(index.Matches("E:/曲库/Alpha",10,StatusMask.Alpha),"Proto default zero is Alpha; nested folders");
        Check(!index.Matches("E:/曲库/Unknown",10,StatusMask.Alpha),"Unknown is not Alpha");
        Check(!index.Matches("E:/曲库/AlphaOther",10,StatusMask.Alpha),"Folder boundary");
        Check(!index.Matches("E:/曲库",10,StatusMask.None),"Empty selection");
        foreach (var format in new uint[] { 1,0x10,0x20,0x40,0x10000,0x20000 }) Check(ChartStatusIndex.ToPlayMode(format)==0,"Key format mapping");
        Check(ChartStatusIndex.ToPlayMode(0x100)==4,"Pad mapping");
        Check(ChartStatusIndex.ToPlayMode(0x4000)==8,"Live mapping");
        Check(ChartStatusIndex.ToPlayMode(0x8000)==9,"Cube mapping");
        foreach(var bad in new byte[][] { new byte[] { 0 },new byte[] { 0x3A,99,1 },new byte[] { 0x80 },new byte[] { 0x7B } })
        {
            try { index.AddRecord(bad); throw new Exception("Accepted invalid metadata"); }
            catch (InvalidDataException) { assertions++; }
        }
        var watch = Stopwatch.StartNew(); var large = new ChartStatusIndex();
        for (var i=0;i<200_000;i++) large.Add($"E:/Large/Song{i/4}/chart{i}.mc",0x40,(uint)(i%3));
        var build = watch.ElapsedMilliseconds; watch.Restart(); var hits = 0;
        for (var pass=0;pass<4;pass++) for(var song=0;song<50_000;song++)
            if(large.Matches($"E:/Large/Song{song}",0,StatusMask.Stable)) hits++;
        Check(hits==200_000,"Large library expected results");
        Console.WriteLine($"PASS: {assertions} index assertions; 200,000 chart records built in {build} ms, 200,000 folder queries in {watch.ElapsedMilliseconds} ms. No chart files or game APIs used.");
    }
    private static byte[] Record(string path,uint mode,uint status)
    {
        using var stream = new MemoryStream();
        void Number(ulong n) { while(n>127) { stream.WriteByte((byte)((n&127)|128));n>>=7; } stream.WriteByte((byte)n); }
        var utf8 = Encoding.UTF8.GetBytes(path);
        Number((7<<3)|2); Number((ulong)utf8.Length); stream.Write(utf8);
        Number(15<<3); Number(mode);
        if(status!=0) { Number(16<<3); Number(status); }
        Number((99<<3)|2); Number(3); stream.Write(new byte[] { 1,2,3 });
        return stream.ToArray();
    }
}
