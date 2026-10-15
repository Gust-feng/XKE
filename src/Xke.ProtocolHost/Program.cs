using Xke.ProtocolHost;

await using var app = XkeProtocolHostApplication.Build(ProtocolHostOptions.FromEnvironment());
await app.RunAsync();
