using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using BetterMalodyV.Manager;
using BetterMalodyV.Shared;

Environment.SetEnvironmentVariable("BETTER_MALODYV_DISABLE_UPDATES",null);
StatusIndexTests.Run();
SharedChartTests.Run();
var assertions = 0;
void Check(bool condition,string label) { if (!condition) throw new Exception(label); assertions++; }
Check(StatusSelection.Matches(StatusMask.All,10),"All includes unknown");
Check(!StatusSelection.Matches(StatusMask.Alpha,10),"Unknown is not Alpha");
Check(!StatusSelection.Matches(StatusMask.Stable,11),"Update is not Stable");
Check(StatusSelection.Matches(StatusMask.Alpha,0),"Alpha includes promotion Alpha");
foreach(var scenario in new[] { "same","older","missing","bad-hash","good","limited" })
{
    var launched = false;
    using var client = new HttpClient(new Fixture(scenario));
    var result = await UpdateService.CheckAndStartAsync(Path.GetTempPath(), testClient:client, testLauncher:start =>
    {
        launched = true;
        Check(start.ArgumentList.Count == 3 && start.ArgumentList[0] == "--update", "Wait-for-exit update protocol");
        Check(start.ArgumentList[2] == Environment.ProcessId.ToString(),"Waits on current manager PID");
        Check(File.ReadAllBytes(start.FileName).SequenceEqual(Fixture.Payload),"Launch only verified download");
        return Process.GetCurrentProcess();
    });
    var expected = scenario == "good" ? UpdateState.Started : scenario is "bad-hash" or "missing" ? UpdateState.Failed : UpdateState.None;
    Check(result.State == expected,scenario + " state");
    Check(launched == (scenario == "good"),scenario + " launch gating");
}
Console.WriteLine($"PASS: {assertions} status/update assertions (real download/hash code, simulated HTTP; no installer executed).");
foreach(var scenario in new[]{"good","bad-hash"})
{
    var started=false;using var client=new HttpClient(new Fixture(scenario));
    var reports=new List<GameUpdateProgress>();
    var result=await UpdateService.CheckAndStartAsync(Path.GetTempPath(),testClient:client,testLauncher:start=>
    {
        started=true;
        Check(start.ArgumentList.Count==4 && start.ArgumentList[0]=="--update-game","Game update selects restart protocol");
        Check(start.ArgumentList[3]=="12345","Game update preserves original game PID");
        return Process.GetCurrentProcess();
    },gameProcessId:12345,gameProgress:new CaptureProgress(reports));
    Check(started==(scenario=="good"),"Game restart installer only runs after checksum verification");
    Check(result.State==(scenario=="good"?UpdateState.Started:UpdateState.Failed),"Game update result");
    Check(reports.Any(p=>p.Stage==GameUpdateStage.Downloading && p.DownloadedBytes==Fixture.Payload.Length && p.Fraction==1),"Real streamed download reports exact received bytes and completion");
    Check(reports.FindIndex(p=>p.Stage==GameUpdateStage.Verifying)>reports.FindLastIndex(p=>p.Stage==GameUpdateStage.Downloading),"Checksum stage follows download completion");
    Check(reports.Any(p=>p.Stage==GameUpdateStage.Ready)==(scenario=="good"),"Failed checksum never reports restart readiness");
}
Check(new GameUpdateProgress(GameUpdateStage.Downloading,"1.1.2",100).Fraction is null,"Unknown download length never produces a false percentage");
Check(!new GameUpdateProgress(GameUpdateStage.Checking).Visible && !new GameUpdateProgress(GameUpdateStage.Failed).Visible,"Quiet checks do not show an unsolicited popup");
var progressRoot=Path.Combine(Path.GetTempPath(),"BetterMalodyV-progress-test-"+Guid.NewGuid().ToString("N"));
try
{
    var expected=new GameUpdateProgress(GameUpdateStage.Downloading,"1.1.2",35,100);
    GameUpdateProtocol.WriteProgress(progressRoot,12345,expected);
    Check(GameUpdateProtocol.ReadProgress(progressRoot,12345)==expected,"Worker progress survives file transport to the game");
    Check(GameUpdateProtocol.ReadProgress(progressRoot,12346) is null,"Progress is scoped to the game process");
    GameUpdateProtocol.WriteProgress(progressRoot,12345,new(GameUpdateStage.Ready,"1.1.2"));
    Check(GameUpdateProtocol.ReadProgress(progressRoot,12345)?.Stage==GameUpdateStage.Ready,"Atomic progress replacement exposes the latest stage");
}
finally {Directory.Delete(progressRoot,true);}
Check(GameUpdateProtocol.IsExpectedGame("E:/TestGame","E:/TestGame/Malody V.exe"),"Expected game executable accepted");
Check(!GameUpdateProtocol.IsExpectedGame("E:/TestGame","E:/Other/Malody V.exe"),"Different installation is never closed");
Check(GameUpdateProtocol.GateName("E:/TestGame/")==GameUpdateProtocol.GateName("e:/testgame"),"One update gate per normalized installation");
Console.WriteLine($"PASS: {assertions} total update/protocol assertions including game-start checks and restart gating.");

sealed class CaptureProgress(List<GameUpdateProgress> values):IProgress<GameUpdateProgress>
{
    public void Report(GameUpdateProgress value)=>values.Add(value);
}

sealed class Fixture(string scenario) : HttpMessageHandler
{
    public static readonly byte[] Payload = "fixture installer bytes"u8.ToArray();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if(path.EndsWith("/latest"))
        {
            if(scenario == "limited") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            var current = typeof(Fixture).Assembly.GetName().Version!;
            var version = scenario == "same" ? current.ToString(3) : scenario == "older" ? "0.9.0" : NextVersion;
            var assets = new List<object> { new { name=$"BetterMalodyV-Setup-{version}.exe", browser_download_url="https://fixture.test/installer" } };
            if(scenario != "missing") assets.Add(new { name="SHA256SUMS.txt", browser_download_url="https://fixture.test/checksum" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { tag_name="v"+version,assets })) });
        }
        var content = path == "/installer" ? (HttpContent)new ByteArrayContent(Payload)
            : new StringContent((scenario == "bad-hash" ? new string('0',64) : Convert.ToHexString(SHA256.HashData(Payload))) + $"  BetterMalodyV-Setup-{NextVersion}.exe\n");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content });
    }
    private static string NextVersion
    {
        get { var v = typeof(Fixture).Assembly.GetName().Version!; return new Version(v.Major,v.Minor,v.Build+1).ToString(); }
    }
}
