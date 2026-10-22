using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using BetterMalodyV.Manager;
using BetterMalodyV.Shared;

Environment.SetEnvironmentVariable("BETTER_MALODYV_DISABLE_UPDATES",null);
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

sealed class Fixture(string scenario) : HttpMessageHandler
{
    public static readonly byte[] Payload = "fixture installer bytes"u8.ToArray();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if(path.EndsWith("/latest"))
        {
            if(scenario == "limited") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            var version = scenario == "same" ? "1.0.0" : scenario == "older" ? "0.9.0" : "1.0.1";
            var assets = new List<object> { new { name=$"BetterMalodyV-Setup-{version}.exe", browser_download_url="https://fixture.test/installer" } };
            if(scenario != "missing") assets.Add(new { name="SHA256SUMS.txt", browser_download_url="https://fixture.test/checksum" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { tag_name="v"+version,assets })) });
        }
        var content = path == "/installer" ? (HttpContent)new ByteArrayContent(Payload)
            : new StringContent((scenario == "bad-hash" ? new string('0',64) : Convert.ToHexString(SHA256.HashData(Payload))) + "  BetterMalodyV-Setup-1.0.1.exe\n");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content });
    }
}
