using System.Reflection;
using Xunit;

namespace Xke.DesktopHost.Tests;

public sealed class DesktopBrandingTests
{
    [Fact]
    public void ProductMetadataUsesTheCurrentDisplayName()
    {
        var assembly = typeof(MainWindow).Assembly;
        Assert.Equal("XKE", assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal("XKE", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
        Assert.Equal("XKE桌面客户端", assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
    }
}
