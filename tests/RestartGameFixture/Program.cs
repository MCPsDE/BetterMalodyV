var gameRoot=AppContext.BaseDirectory;
var marker=Path.Combine(gameRoot,"fixture-starts.txt");
var count=File.Exists(marker)?int.Parse(File.ReadAllText(marker)):0;
File.WriteAllText(marker,(count+1).ToString());
if(count>0)return;
File.WriteAllText(Path.Combine(gameRoot,"fixture-pid.txt"),Environment.ProcessId.ToString());
var request=Path.Combine(gameRoot,"BetterMalodyV",".updates",$"quit-{Environment.ProcessId}.request");
var deadline=DateTime.UtcNow.AddMinutes(2);
while(DateTime.UtcNow<deadline)
{
    if(File.Exists(request)){File.WriteAllText(Path.Combine(gameRoot,"fixture-graceful-exit.txt"),"requested");Thread.Sleep(2500);return;}
    Thread.Sleep(100);
}
Environment.ExitCode=2;
