using Xke.WebHost;

var app = XkeWebHostApplication.Create(args, new XkeWebHostOptions
{
    HostMode = "standalone",
});

await app.RunAsync();

public partial class Program;
