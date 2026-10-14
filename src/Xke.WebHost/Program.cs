using Xke.WebHost;

var app = XkeWebHostApplication.Create(args, new XkeWebHostOptions
{
    HostMode = "standalone",
    AllowedFrontendOrigins = (Environment.GetEnvironmentVariable("XKE_FRONTEND_ORIGINS") ?? "")
        .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
});

await app.RunAsync();

public partial class Program;
