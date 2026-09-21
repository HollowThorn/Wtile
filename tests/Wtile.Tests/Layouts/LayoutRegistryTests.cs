using Wtile.Layouts;

namespace Wtile.Tests.Layouts;

public class LayoutRegistryTests
{
    [Theory]
    [InlineData("master-stack", "[]=")]
    [InlineData("master-stack-right", "=[]")]
    [InlineData("monocle", "[M]")]
    [InlineData("centered-master", "|M|")]
    [InlineData("vertical", "=")]
    [InlineData("deck", "[D]")]
    [InlineData("dwindle", "[\\]")]
    public void CreateDefault_RegistersBuiltinLayouts(string name, string symbol)
    {
        LayoutRegistry registry = LayoutRegistry.CreateDefault();

        Assert.True(registry.TryGet(name, out ILayout layout));
        Assert.Equal(symbol, layout.Symbol);
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalse()
    {
        LayoutRegistry registry = LayoutRegistry.CreateDefault();
        Assert.False(registry.TryGet("does-not-exist", out _));
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        LayoutRegistry registry = LayoutRegistry.CreateDefault();
        Assert.True(registry.TryGet("MASTER-STACK", out _));
    }
}
