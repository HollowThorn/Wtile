using Wtile.Core;

namespace Wtile.Tests.Core;

public class ByteRateFormatterTests
{
    [Theory]
    [InlineData(0, "0B")]
    [InlineData(512, "512B")]
    [InlineData(1023, "1023B")]
    [InlineData(1024, "1.0K")]
    [InlineData(1536, "1.5K")]
    [InlineData(1048576, "1.0M")]
    [InlineData(1572864, "1.5M")]
    [InlineData(1073741824, "1.0G")]
    public void Format_MatchesExpected(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, ByteRateFormatter.Format(bytesPerSecond));
    }

    [Fact]
    public void Format_NegativeClampsToZero()
    {
        Assert.Equal("0B", ByteRateFormatter.Format(-100));
    }
}
