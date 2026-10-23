using BetterMalodyV.Shared;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

internal static class SharedChartTests
{
    internal static void Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"BetterMalodyV-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var count=0;void Check(bool b,string m){if(!b)throw new Exception(m);count++;}
        try
        {
            var chart=Encoding.UTF8.GetBytes("{\"meta\":{\"version\":\"new\"}}");
            var hash=Convert.ToHexString(MD5.HashData(chart)).ToLowerInvariant();
            var archive=Path.Combine(root,"shared.zip");
            void Zip(string name,byte[] bytes)
            {
                using var zip=ZipFile.Open(archive,ZipArchiveMode.Create);var entry=zip.CreateEntry(name);
                using var s=entry.Open();s.Write(bytes);
            }
            Zip("HostSong/chart.mc",chart);
            var library=Path.Combine(root,"chart");var song=Path.Combine(library,"HostSong");Directory.CreateDirectory(song);
            var target=Path.Combine(song,"chart.mc");File.WriteAllText(target,"old");File.WriteAllText(Path.Combine(song,"other.mc"),"unrelated");
            using(var install=SharedChartInstall.Prepare(archive,library,hash))
            {
                Check(File.ReadAllBytes(target).SequenceEqual(chart),"Existing stale chart overwritten");
                Check(install.SongPath==song,"Reuse archive folder");
            }
            Check(File.ReadAllText(target)=="old","Failure/cancel restores original chart");
            using(var install=SharedChartInstall.Prepare(archive,library,hash)){install.Commit();}
            Check(File.ReadAllBytes(target).SequenceEqual(chart),"Successful share retained");
            Check(File.ReadAllText(Path.Combine(song,"other.mc"))=="unrelated","Other charts retained");
            var oldNamed=Path.Combine(song,"old-name.mc");File.WriteAllText(oldNamed,"old same chart ID");
            using(var install=SharedChartInstall.Prepare(archive,library,hash,song,oldNamed)){Check(!File.Exists(oldNamed),"Same chart ID with changed filename retires old file");}
            Check(File.ReadAllText(oldNamed)=="old same chart ID","Changed filename rollback restores old chart");
            try{using var wrong=SharedChartInstall.Prepare(archive,library,new string('f',32));throw new Exception("Wrong hash accepted");}
            catch(InvalidDataException){count++;}
            var custom=Path.Combine(root,"custom","OriginalName");Directory.CreateDirectory(custom);
            using(var install=SharedChartInstall.Prepare(archive,library,hash,custom)){Check(File.Exists(Path.Combine(custom,"chart.mc")),"Same song ID replaces custom existing folder");install.Commit();}
            File.Delete(archive);Zip("../outside.mc",chart);
            try{using var invalid=SharedChartInstall.Prepare(archive,library,hash);throw new Exception("Traversal accepted");}
            catch(InvalidDataException){count++;}
            Check(!File.Exists(Path.Combine(root,"outside.mc")),"Traversal creates no outside file");
            File.Delete(archive);Zip("HostSong/chart.mc",chart);
            var fresh=Path.Combine(root,"fresh");
            using(var install=SharedChartInstall.Prepare(archive,fresh,hash)){}
            Check(!File.Exists(Path.Combine(fresh,"HostSong","chart.mc")),"Canceled fresh import removes created file");
            Console.WriteLine($"PASS: {count} shared-chart archive/replace/rollback assertions.");
        }
        finally{Directory.Delete(root,true);}
    }
}
