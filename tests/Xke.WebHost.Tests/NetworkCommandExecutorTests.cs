using System.Runtime.InteropServices;
using System.Text;
using Xke.Core.Network;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NetworkCommandExecutorTests
{
    [Fact]
    public async Task NativeCommandOutput_PreservesLocalizedStdoutAndStderr()
    {
        if (!OperatingSystem.IsWindows()) return;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(checked((int)GetOEMCP()));
        const string message = "本地网络命令测试 192.0.2.8";
        var expected = encoding.GetString(encoding.GetBytes(message));
        var executor = new ProcessNetworkCommandExecutor();
        var result = await executor.RunAsync("cmd.exe", ["/d", "/c", $"echo {message}&echo {message} 1>&2"], TimeSpan.FromSeconds(5));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected, result.Stdout.Trim());
        Assert.Equal(expected, result.Stderr.Trim());
        Assert.DoesNotContain('�', result.Stdout);
    }

    [Theory]
    [InlineData("Ethernet adapter Fixture", "IPv4 Address", "Default Gateway")]
    [InlineData("以太网适配器 合成网卡", "IPv4 地址", "默认网关")]
    public void IpConfigParser_ExtractsActualAddressFromSupportedLocalizedFields(string header, string address, string gateway)
    {
        var text = $"{header}:\n   {address} : 192.0.2.8\n   {gateway} : 192.0.2.1\n";
        var adapter = Assert.Single(NetworkService.ParseIpConfigOutput(text));
        Assert.Equal("192.0.2.8", adapter["ipv4"]);
        Assert.Contains("192.0.2.1", Assert.IsType<List<string>>(adapter["default_gateway"]));
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}
